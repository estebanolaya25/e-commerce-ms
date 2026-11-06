using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Assistant_Api.Modelos;

namespace Assistant_Api.Servicios;

/// <summary>
/// Estado de una conversación mientras el modelo usa herramientas: quién pregunta, qué escribió en sus últimos mensajes
/// (para validar que de verdad pidió una acción y eligió sus opciones) y qué acciones se le propusieron.
/// </summary>
public class SesionHerramientas(ContextoUsuario usuario, IEnumerable<string> mensajesRecientesDelCliente)
{
    public const int MaxAcciones = 3;

    public ContextoUsuario Usuario { get; } = usuario;
    public List<AccionPropuesta> Acciones { get; } = [];
    public string TextoDelCliente { get; } = string.Join('\n', mensajesRecientesDelCliente);
}

/// <summary>
/// Herramientas que el modelo puede pedir cuando el cliente inició sesión. Las de consulta leen Pedidos y Carrito con
/// el token del cliente. Las de acción ("proponer_…") no cambian nada: validan y dejan una propuesta que el cliente
/// confirma con un botón; quien ejecuta es el frontend, con la sesión del cliente, por la API de siempre.
/// </summary>
public class HerramientasAsistente(
    PedidosCliente pedidos,
    CarritoCliente carrito,
    CatalogoCliente catalogo,
    InventarioCliente inventario,
    ILogger<HerramientasAsistente> logger) : IConjuntoHerramientas
{
    private const int PedidosRecientes = 5;
    private const int MaxUnidades = 10;
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    // Colombia no tiene horario de verano: UTC-5 fijo, sin depender de la base de zonas horarias del contenedor.
    private static readonly TimeSpan HoraColombia = TimeSpan.FromHours(-5);

    internal static readonly Dictionary<string, string> EstadosPedido = new()
    {
        ["PendientePago"] = "Pendiente de pago",
        ["PagoEnProceso"] = "Pago en proceso",
        ["Pagado"] = "Pagado",
        ["EnPreparacion"] = "En preparación",
        ["Enviado"] = "Enviado",
        ["Entregado"] = "Entregado",
        ["Cancelado"] = "Cancelado",
        ["Expirado"] = "Expirado"
    };

    public static readonly JsonElement Definiciones = JsonDocument.Parse("""
        [
          {
            "type": "function",
            "function": {
              "name": "consultar_mis_pedidos",
              "description": "Lista los 5 pedidos más recientes del cliente: número, fecha, estado, total y unidades. Úsala cuando pregunte por sus pedidos o compras sin dar un número.",
              "parameters": { "type": "object", "properties": {}, "required": [] }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "consultar_pedido",
              "description": "Detalle de un pedido del cliente por su número (formato PED-AAAA-NNNNNN): productos, totales, estado, historial de estados y motivo de un pago rechazado.",
              "parameters": {
                "type": "object",
                "properties": { "numero": { "type": "string", "description": "Número del pedido, p. ej. PED-2026-000123" } },
                "required": ["numero"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "consultar_carrito",
              "description": "Productos que el cliente tiene en su carrito, con cantidades, precios, total y avisos de disponibilidad.",
              "parameters": { "type": "object", "properties": {}, "required": [] }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "proponer_agregar_al_carrito",
              "description": "Prepara agregar un producto al carrito. NO lo agrega: el cliente verá un botón para confirmar. Úsala solo si el cliente pide agregar o comprar un producto concreto.",
              "parameters": {
                "type": "object",
                "properties": {
                  "slug": { "type": "string", "description": "Slug del producto (de <productos> o de la conversación)" },
                  "cantidad": { "type": "integer", "description": "Unidades, de 1 a 10" },
                  "talla": { "type": "string", "description": "Talla elegida, si el producto tiene tallas" },
                  "color": { "type": "string", "description": "Color elegido, si el producto tiene colores" },
                  "otra_opcion": { "type": "string", "description": "Otra opción de la variante (p. ej. almacenamiento o memoria)" }
                },
                "required": ["slug", "cantidad"]
              }
            }
          },
          {
            "type": "function",
            "function": {
              "name": "proponer_cancelar_pedido",
              "description": "Prepara cancelar un pedido del cliente que está pendiente de pago. NO lo cancela: el cliente verá un botón para confirmar.",
              "parameters": {
                "type": "object",
                "properties": { "numero": { "type": "string", "description": "Número del pedido, p. ej. PED-2026-000123" } },
                "required": ["numero"]
              }
            }
          }
        ]
        """).RootElement;

    // Todas las herramientas del cliente aplican a cualquier usuario con sesión: Pedidos y Carrito filtran por su token.
    public JsonElement DefinicionesPara(SesionHerramientas sesion) => Definiciones;

    /// <summary>Ejecuta una herramienta y devuelve el resultado como JSON para el modelo. Nunca lanza por errores de datos.</summary>
    public async Task<string> EjecutarAsync(string nombre, JsonElement argumentos, SesionHerramientas sesion, CancellationToken ct)
    {
        try
        {
            object resultado = nombre switch
            {
                "consultar_mis_pedidos" => await MisPedidosAsync(sesion, ct),
                "consultar_pedido" => await PedidoAsync(Texto(argumentos, "numero"), sesion, ct),
                "consultar_carrito" => await CarritoAsync(sesion, ct),
                "proponer_agregar_al_carrito" => await ProponerAgregarAsync(argumentos, sesion, ct),
                "proponer_cancelar_pedido" => await ProponerCancelarAsync(Texto(argumentos, "numero"), sesion, ct),
                _ => Error($"La herramienta {nombre} no existe.")
            };
            return JsonSerializer.Serialize(resultado, JsonSerializerOptions.Web);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return JsonSerializer.Serialize(Error("La sesión del cliente venció: debe volver a iniciar sesión."));
        }
        catch (Exception ex) when (Fallos.EsTransitorio(ex) && !ct.IsCancellationRequested)
        {
            logger.LogWarning("La herramienta {Herramienta} falló: {Mensaje}", nombre, ex.Message);
            return JsonSerializer.Serialize(Error("Ese servicio de la tienda no responde en este momento; pide al cliente intentar en unos minutos."));
        }
    }

    private async Task<object> MisPedidosAsync(SesionHerramientas sesion, CancellationToken ct)
    {
        var pagina = await pedidos.RecientesAsync(sesion.Usuario.AccessToken, PedidosRecientes, ct);
        if (pagina.Items.Count == 0)
        {
            return new { pedidos = Array.Empty<object>(), nota = "El cliente no tiene pedidos." };
        }
        return new
        {
            totalPedidos = pagina.Total,
            pedidos = pagina.Items.Select(p => new
            {
                numero = p.Numero,
                fecha = Fecha(p.CreatedAt),
                estado = Estado(p.Estado),
                total = Pesos(p.Total),
                unidades = p.Unidades,
                pagarAntesDe = p.Estado == "PendientePago" ? Fecha(p.ExpiraEn) : null
            })
        };
    }

    private async Task<object> PedidoAsync(string? numero, SesionHerramientas sesion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return Error("Falta el número del pedido. Pídeselo al cliente o usa consultar_mis_pedidos.");
        }

        var p = await pedidos.PorNumeroAsync(sesion.Usuario.AccessToken, numero.Trim(), ct);
        if (p is null)
        {
            return Error($"El cliente no tiene un pedido con el número {numero}.");
        }

        var minutosParaPagar = (int)Math.Floor((p.ExpiraEn - DateTime.UtcNow).TotalMinutes);
        return new
        {
            numero = p.Numero,
            fecha = Fecha(p.CreatedAt),
            estado = Estado(p.Estado),
            productos = p.Items.Select(i => new
            {
                nombre = i.Nombre,
                opciones = i.Opciones,
                cantidad = i.Cantidad,
                precioUnitario = Pesos(i.PrecioUnitario),
                subtotal = Pesos(i.Subtotal)
            }),
            subtotal = Pesos(p.Subtotal),
            envio = Pesos(p.CostoEnvio),
            total = Pesos(p.Total),
            ciudadEnvio = p.Direccion?.Ciudad,
            motivoUltimoPagoRechazado = p.UltimoErrorPago,
            minutosRestantesParaPagar = p.Estado == "PendientePago" ? Math.Max(0, minutosParaPagar) : (int?)null,
            historial = p.Historial.OrderBy(h => h.CreatedAt).Select(h => new
            {
                fecha = Fecha(h.CreatedAt),
                estado = Estado(h.EstadoNuevo),
                motivo = h.Motivo
            })
        };
    }

    private async Task<object> CarritoAsync(SesionHerramientas sesion, CancellationToken ct)
    {
        var c = await carrito.ObtenerAsync(sesion.Usuario.AccessToken, ct);
        if (c.Items.Count == 0)
        {
            return new { productos = Array.Empty<object>(), nota = "El carrito está vacío." };
        }
        return new
        {
            productos = c.Items.Select(i => new
            {
                nombre = i.Nombre ?? i.Sku,
                slug = i.Slug,
                opciones = i.Opciones is { Count: > 0 } o ? string.Join(", ", o.Select(kv => $"{kv.Key}: {kv.Value}")) : null,
                cantidad = i.Cantidad,
                precioUnitario = i.PrecioUnitario is decimal precio ? Pesos(precio) : null,
                subtotal = i.Subtotal is decimal subtotal ? Pesos(subtotal) : null,
                estado = i.Estado
            }),
            unidades = c.Unidades,
            total = Pesos(c.Total),
            puedeComprar = c.PuedeComprar,
            avisos = c.Avisos
        };
    }

    // Intención explícita del cliente en sus últimos mensajes. El modelo (o un texto malicioso en un nombre de producto)
    // no puede proponer acciones que el cliente no pidió: el servidor lo verifica, no solo el prompt.
    private static readonly Regex PideAgregar = new(
        @"\b(agreg\w*|anad\w*|pon(me|lo|la|los|las|er)?|met(e|elo|ela|er)|compr(ar|o|alo|ala|arlo|arla)|llev(o|ar|alo|ala|armelo|armela)|sum(a|ar|alo|ala))\b",
        RegexOptions.Compiled);

    private static readonly Regex PideCancelar = new(@"\b(cancel\w*|anul\w*)\b", RegexOptions.Compiled);

    private async Task<object> ProponerAgregarAsync(JsonElement argumentos, SesionHerramientas sesion, CancellationToken ct)
    {
        if (sesion.Acciones.Count >= SesionHerramientas.MaxAcciones)
        {
            return Error("Ya hay varias acciones por confirmar; pide al cliente que las confirme primero.");
        }
        if (!PideAgregar.IsMatch(Normalizar(sesion.TextoDelCliente)))
        {
            return Error("El cliente no pidió agregar nada al carrito: no propongas esta acción. Responde solo lo que preguntó.");
        }

        var slug = Texto(argumentos, "slug");
        var cantidad = Entero(argumentos, "cantidad") ?? 1;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Error("Falta el slug del producto.");
        }
        if (cantidad is < 1 or > MaxUnidades)
        {
            return Error($"La cantidad debe estar entre 1 y {MaxUnidades}.");
        }

        var producto = await catalogo.ObtenerAsync(slug.Trim(), ct);
        if (producto is null)
        {
            return Error($"No existe un producto a la venta con el slug {slug}.");
        }

        var variantes = producto.Variantes.Where(v => v.Activa).ToList();
        var elegidas = new[] { Texto(argumentos, "talla"), Texto(argumentos, "color"), Texto(argumentos, "otra_opcion") }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToList();
        var coincidentes = FiltrarVariantes(variantes, elegidas);

        if (coincidentes.Count != 1)
        {
            // Falta elegir (o lo elegido no existe): se devuelven las combinaciones para que el modelo pregunte.
            return new
            {
                error = coincidentes.Count == 0
                    ? "No hay una variante con esas opciones."
                    : "El producto tiene varias variantes: falta que el cliente elija.",
                producto = producto.Nombre,
                opcionesDisponibles = variantes.Take(20).Select(v => DescribirOpciones(v.Opciones)).ToList()
            };
        }

        var variante = coincidentes[0];

        // Cada opción con varias alternativas (talla, color…) debe haberla escrito el cliente: el modelo no elige por él.
        var noElegidas = OpcionesNoElegidas(variante, variantes, sesion.TextoDelCliente);
        if (noElegidas.Count > 0)
        {
            return new
            {
                error = $"El cliente no ha elegido: {string.Join(", ", noElegidas)}. Pregúntale antes de proponer.",
                producto = producto.Nombre,
                opcionesDisponibles = variantes.Take(20).Select(v => DescribirOpciones(v.Opciones)).ToList()
            };
        }

        var disponibles = await inventario.DisponiblesAsync([variante.Sku], ct);
        var disponible = disponibles.GetValueOrDefault(variante.Sku);
        if (disponible < cantidad)
        {
            return Error(disponible == 0
                ? $"{producto.Nombre} ({DescribirOpciones(variante.Opciones)}) está agotado."
                : $"Solo quedan {disponible} unidades de {producto.Nombre} ({DescribirOpciones(variante.Opciones)}).");
        }

        var opciones = DescribirOpciones(variante.Opciones);
        var descripcion = $"Agregar {cantidad} × {producto.Nombre}{(opciones.Length > 0 ? $" ({opciones})" : "")} al carrito — {Pesos(variante.Precio * cantidad)}";
        sesion.Acciones.Add(new AccionPropuesta(AccionPropuesta.AgregarAlCarrito, descripcion,
            Sku: variante.Sku, Cantidad: cantidad, Slug: producto.Slug));

        return new
        {
            propuestaCreada = true,
            descripcion,
            nota = "Todavía NO está en el carrito: el cliente debe pulsar «Confirmar» debajo del mensaje."
        };
    }

    private async Task<object> ProponerCancelarAsync(string? numero, SesionHerramientas sesion, CancellationToken ct)
    {
        if (sesion.Acciones.Count >= SesionHerramientas.MaxAcciones)
        {
            return Error("Ya hay varias acciones por confirmar; pide al cliente que las confirme primero.");
        }
        if (!PideCancelar.IsMatch(Normalizar(sesion.TextoDelCliente)))
        {
            return Error("El cliente no pidió cancelar un pedido: no propongas esta acción.");
        }
        if (string.IsNullOrWhiteSpace(numero))
        {
            return Error("Falta el número del pedido.");
        }

        var p = await pedidos.PorNumeroAsync(sesion.Usuario.AccessToken, numero.Trim(), ct);
        if (p is null)
        {
            return Error($"El cliente no tiene un pedido con el número {numero}.");
        }
        if (p.Estado != "PendientePago")
        {
            return Error($"El pedido {p.Numero} está «{Estado(p.Estado)}» y solo se pueden cancelar los pedidos pendientes de pago.");
        }

        var descripcion = $"Cancelar el pedido {p.Numero} ({Pesos(p.Total)})";
        sesion.Acciones.Add(new AccionPropuesta(AccionPropuesta.CancelarPedido, descripcion, PedidoId: p.PedidoId, Numero: p.Numero));
        return new
        {
            propuestaCreada = true,
            descripcion,
            nota = "Todavía NO está cancelado: el cliente debe pulsar «Confirmar» debajo del mensaje."
        };
    }

    /// <summary>
    /// Variantes cuyas opciones contienen todos los valores elegidos (sin tildes ni mayúsculas). Primero exacto
    /// ("Azul" no debe elegir "Azul marino"); si nada coincide exacto, por contenido ("marino" → "Azul marino").
    /// Sin nada elegido se devuelven todas: si el producto tiene una sola variante, no hace falta elegir.
    /// </summary>
    private static List<VarianteCatalogo> FiltrarVariantes(List<VarianteCatalogo> variantes, List<string> elegidas)
    {
        if (elegidas.Count == 0)
        {
            return variantes;
        }

        var buscadas = elegidas.Select(Normalizar).ToList();
        List<VarianteCatalogo> Coinciden(Func<string, string, bool> igual) => variantes
            .Where(v => buscadas.All(b => (v.Opciones ?? []).Values.Any(valor => igual(Normalizar(valor), b))))
            .ToList();

        var exactas = Coinciden((valor, buscado) => valor == buscado);
        return exactas.Count > 0 ? exactas : Coinciden((valor, buscado) => valor.Contains(buscado));
    }

    /// <summary>Claves de opción (talla, color…) con más de un valor en el producto cuyo valor el cliente no escribió.</summary>
    private static List<string> OpcionesNoElegidas(VarianteCatalogo variante, List<VarianteCatalogo> variantes, string textoCliente)
    {
        var texto = Normalizar(textoCliente);
        var faltan = new List<string>();
        foreach (var (clave, valor) in variante.Opciones ?? [])
        {
            var alternativas = variantes
                .Select(v => v.Opciones?.GetValueOrDefault(clave))
                .Where(v => v is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var mencionado = Regex.IsMatch(texto, $@"(?<![\w]){Regex.Escape(Normalizar(valor))}(?![\w])");
            if (alternativas > 1 && !mencionado)
            {
                faltan.Add(clave);
            }
        }
        return faltan;
    }

    internal static string DescribirOpciones(Dictionary<string, string>? opciones) =>
        opciones is null ? "" : string.Join(", ", opciones.Select(kv => $"{Capitalizar(kv.Key)} {kv.Value}"));

    private static string Capitalizar(string texto) => texto.Length == 0 ? texto : char.ToUpper(texto[0]) + texto[1..];

    internal static object Error(string mensaje) => new { error = mensaje };

    internal static string Estado(string estado) => EstadosPedido.GetValueOrDefault(estado, estado);

    internal static string Pesos(decimal valor) => valor.ToString("C0", Colombia);

    internal static string Fecha(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(HoraColombia).ToString("d 'de' MMMM 'de' yyyy, h:mm tt", Colombia);

    internal static string? Texto(JsonElement argumentos, string clave) =>
        argumentos.ValueKind == JsonValueKind.Object && argumentos.TryGetProperty(clave, out var valor)
            ? valor.ValueKind switch
            {
                JsonValueKind.String => valor.GetString(),
                JsonValueKind.Number => valor.GetRawText(),
                _ => null
            }
            : null;

    internal static int? Entero(JsonElement argumentos, string clave)
    {
        if (argumentos.ValueKind != JsonValueKind.Object || !argumentos.TryGetProperty(clave, out var valor))
        {
            return null;
        }
        return valor.ValueKind switch
        {
            JsonValueKind.Number when valor.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(valor.GetString(), out var n) => n,
            _ => null
        };
    }

    internal static string Normalizar(string texto)
    {
        var builder = new StringBuilder();
        foreach (var c in texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }
}
