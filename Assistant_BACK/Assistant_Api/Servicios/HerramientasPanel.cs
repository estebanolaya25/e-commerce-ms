using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Assistant_Api.Modelos;
using static Assistant_Api.Servicios.HerramientasAsistente;

namespace Assistant_Api.Servicios;

/// <summary>
/// Herramientas del asistente del panel interno (/admin). Cada una se ofrece solo a los roles que pueden usar el
/// endpoint que llama, y todas llaman con el token del empleado: aunque el modelo pidiera otra cosa, Pedidos, Pagos e
/// Inventario aplican sus propias reglas (por ejemplo, Bodega solo ve pedidos pagados y sin el correo del cliente).
/// La única acción, avanzar un pedido, es una propuesta que el empleado confirma con un botón.
/// </summary>
public class HerramientasPanel(
    PanelPedidosCliente pedidos,
    PagosCliente pagos,
    StockCliente stock,
    CatalogoCliente catalogo,
    ProductosIndiceService productos,
    ILogger<HerramientasPanel> logger) : IConjuntoHerramientas
{
    private const int MaxPedidos = 10;
    private const int MaxStock = 20;
    private const int MaxMovimientos = 10;

    private static readonly string[] RolesPedidos = [RolesTienda.Administrador, RolesTienda.Soporte, RolesTienda.Bodega];
    private static readonly string[] RolesPagos = [RolesTienda.Administrador, RolesTienda.Soporte];
    private static readonly string[] RolesInventario = [RolesTienda.Administrador, RolesTienda.Bodega];
    private static readonly string[] RolesDespacho = [RolesTienda.Administrador, RolesTienda.Bodega];
    private static readonly string[] RolesCatalogo = [RolesTienda.Administrador, RolesTienda.Catalogo];

    // Cada estado de despacho solo puede avanzar al siguiente (las mismas reglas que aplica Pedidos).
    private static readonly Dictionary<string, string> SiguienteEstado = new()
    {
        ["Pagado"] = "EnPreparacion",
        ["EnPreparacion"] = "Enviado",
        ["Enviado"] = "Entregado"
    };

    private static readonly Regex PideAvanzar = new(
        @"\b(despach\w*|envi\w*|prepar\w*|entreg\w*|avanz\w*|marc\w*|pas(a|ar|alo|ala|elo|ela)|cambi\w*|muev\w*|mover\w*)\b",
        RegexOptions.Compiled);

    private record Herramienta(string[] Roles, string Definicion);

    private static readonly Dictionary<string, Herramienta> Catalogo = new()
    {
        ["buscar_pedidos"] = new(RolesPedidos, """
            { "type": "function", "function": {
              "name": "buscar_pedidos",
              "description": "Busca pedidos de la tienda (los 10 más recientes que coincidan). Filtra por estado y/o número.",
              "parameters": { "type": "object", "properties": {
                "estado": { "type": "string", "enum": ["PendientePago", "PagoEnProceso", "Pagado", "EnPreparacion", "Enviado", "Entregado", "Cancelado", "Expirado"] },
                "numero": { "type": "string", "description": "Número o parte del número, p. ej. PED-2026-000123" } },
                "required": [] } } }
            """),
        ["consultar_pedido"] = new(RolesPedidos, """
            { "type": "function", "function": {
              "name": "consultar_pedido",
              "description": "Detalle de un pedido por su número: cliente, productos, totales, ciudad de envío, historial de estados y último error de pago.",
              "parameters": { "type": "object", "properties": { "numero": { "type": "string" } }, "required": ["numero"] } } }
            """),
        ["consultar_pagos_pedido"] = new(RolesPagos, """
            { "type": "function", "function": {
              "name": "consultar_pagos_pedido",
              "description": "Intentos de pago de un pedido: estado, monto, tarjeta (marca y últimos 4), referencia, motivo de rechazo y reembolso.",
              "parameters": { "type": "object", "properties": { "numero": { "type": "string" } }, "required": ["numero"] } } }
            """),
        ["consultar_stock_bajo"] = new(RolesInventario, """
            { "type": "function", "function": {
              "name": "consultar_stock_bajo",
              "description": "SKU con stock disponible en o por debajo del mínimo (o solo los agotados), con nombre del producto y opciones.",
              "parameters": { "type": "object", "properties": { "solo_agotados": { "type": "boolean" } }, "required": [] } } }
            """),
        ["consultar_stock"] = new(RolesInventario, """
            { "type": "function", "function": {
              "name": "consultar_stock",
              "description": "Stock de un SKU (físico, reservado, disponible y mínimo) y sus últimos movimientos.",
              "parameters": { "type": "object", "properties": { "sku": { "type": "string" } }, "required": ["sku"] } } }
            """),
        ["proponer_avanzar_pedido"] = new(RolesDespacho, """
            { "type": "function", "function": {
              "name": "proponer_avanzar_pedido",
              "description": "Prepara avanzar un pedido al siguiente estado de despacho (Pagado → EnPreparacion → Enviado → Entregado). NO lo cambia: el empleado verá un botón para confirmar.",
              "parameters": { "type": "object", "properties": {
                "numero": { "type": "string" },
                "estado": { "type": "string", "enum": ["EnPreparacion", "Enviado", "Entregado"] } },
                "required": ["numero", "estado"] } } }
            """),
        ["buscar_productos"] = new(RolesCatalogo, """
            { "type": "function", "function": {
              "name": "buscar_productos",
              "description": "Busca productos activos del catálogo por descripción (búsqueda semántica): nombre, slug, marca, categoría, precio, colores, tallas y disponibilidad.",
              "parameters": { "type": "object", "properties": { "texto": { "type": "string" } }, "required": ["texto"] } } }
            """)
    };

    public JsonElement DefinicionesPara(SesionHerramientas sesion)
    {
        var lista = new JsonArray();
        foreach (var herramienta in Catalogo.Values.Where(h => sesion.Usuario.TieneAlgunRol(h.Roles)))
        {
            lista.Add(JsonNode.Parse(herramienta.Definicion));
        }
        return JsonSerializer.SerializeToElement(lista);
    }

    public async Task<string> EjecutarAsync(string nombre, JsonElement argumentos, SesionHerramientas sesion, CancellationToken ct)
    {
        // Un modelo podría nombrar una herramienta que no se le ofreció: se vuelve a comprobar el rol aquí.
        if (!Catalogo.TryGetValue(nombre, out var herramienta) || !sesion.Usuario.TieneAlgunRol(herramienta.Roles))
        {
            return JsonSerializer.Serialize(Error($"La herramienta {nombre} no está disponible para este usuario."));
        }

        var token = sesion.Usuario.AccessToken;
        try
        {
            object resultado = nombre switch
            {
                "buscar_pedidos" => await BuscarPedidosAsync(token, Texto(argumentos, "estado"), Texto(argumentos, "numero"), ct),
                "consultar_pedido" => await PedidoAsync(token, Texto(argumentos, "numero"), ct),
                "consultar_pagos_pedido" => await PagosAsync(token, Texto(argumentos, "numero"), ct),
                "consultar_stock_bajo" => await StockBajoAsync(token, Booleano(argumentos, "solo_agotados"), ct),
                "consultar_stock" => await StockAsync(token, Texto(argumentos, "sku"), ct),
                "proponer_avanzar_pedido" => await ProponerAvanzarAsync(Texto(argumentos, "numero"), Texto(argumentos, "estado"), sesion, ct),
                "buscar_productos" => await BuscarProductosAsync(Texto(argumentos, "texto"), ct),
                _ => Error($"La herramienta {nombre} no existe.")
            };
            return JsonSerializer.Serialize(resultado, JsonSerializerOptions.Web);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized)
        {
            return JsonSerializer.Serialize(Error("La sesión venció: el usuario debe volver a iniciar sesión."));
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden)
        {
            return JsonSerializer.Serialize(Error("El rol del usuario no tiene permiso para esa consulta."));
        }
        catch (Exception ex) when (Fallos.EsTransitorio(ex) && !ct.IsCancellationRequested)
        {
            logger.LogWarning("La herramienta del panel {Herramienta} falló: {Mensaje}", nombre, ex.Message);
            return JsonSerializer.Serialize(Error("Ese servicio no responde en este momento; intenta en unos minutos."));
        }
    }

    private async Task<object> BuscarPedidosAsync(string token, string? estado, string? numero, CancellationToken ct)
    {
        var codigo = CodigoEstado(estado);
        if (estado is not null && codigo is null)
        {
            return Error($"Estado desconocido: {estado}.");
        }
        var pagina = await pedidos.BuscarAsync(token, codigo, numero, MaxPedidos, ct);
        return new
        {
            totalCoincidencias = pagina.Total,
            mostrados = pagina.Items.Count,
            pedidos = pagina.Items.Select(p => new
            {
                numero = p.Numero,
                fecha = Fecha(p.CreatedAt),
                estado = Estado(p.Estado),
                total = Pesos(p.Total),
                unidades = p.Unidades
            })
        };
    }

    private async Task<object> PedidoAsync(string token, string? numero, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return Error("Falta el número del pedido.");
        }
        var p = await pedidos.PorNumeroAsync(token, numero, ct);
        if (p is null)
        {
            return Error($"No hay un pedido {numero} visible para este usuario.");
        }
        return new
        {
            numero = p.Numero,
            // Pedidos no envía el correo a Bodega: se dice explícitamente para que el modelo no responda "no tiene correo".
            cliente = p.UsuarioEmail ?? "(correo no visible para este rol)",
            fecha = Fecha(p.CreatedAt),
            estado = Estado(p.Estado),
            puedeAvanzarA_aunNoAplicado = SiguienteEstado.TryGetValue(p.Estado, out var siguiente) ? Estado(siguiente) : null,
            productos = p.Items.Select(i => new { nombre = i.Nombre, opciones = i.Opciones, cantidad = i.Cantidad, subtotal = Pesos(i.Subtotal) }),
            subtotal = Pesos(p.Subtotal),
            envio = Pesos(p.CostoEnvio),
            total = Pesos(p.Total),
            ciudadEnvio = p.Direccion?.Ciudad,
            ultimoErrorPago = p.UltimoErrorPago,
            historial = p.Historial.OrderBy(h => h.CreatedAt).Select(h => new { fecha = Fecha(h.CreatedAt), estado = Estado(h.EstadoNuevo), motivo = h.Motivo })
        };
    }

    private async Task<object> PagosAsync(string token, string? numero, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return Error("Falta el número del pedido.");
        }
        var pedido = await pedidos.PorNumeroAsync(token, numero, ct);
        if (pedido is null)
        {
            return Error($"No hay un pedido {numero} visible para este usuario.");
        }
        var intentos = await pagos.DelPedidoAsync(token, pedido.PedidoId, ct);
        return new
        {
            pedido = pedido.Numero,
            totalPedido = Pesos(pedido.Total),
            pagos = intentos.OrderBy(p => p.CreatedAt).Select(p => new
            {
                estado = p.Estado,
                monto = Pesos(p.Monto),
                tarjeta = $"{p.Marca} terminada en {p.Ultimos4}",
                referencia = p.Referencia,
                motivoRechazo = p.MotivoRechazo,
                fecha = Fecha(p.CreatedAt),
                procesado = p.ProcesadoEn is DateTime procesado ? Fecha(procesado) : null,
                reembolsado = p.ReembolsadoEn is DateTime reembolso ? Fecha(reembolso) : null
            }),
            nota = intentos.Count == 0 ? "El pedido no tiene intentos de pago." : null
        };
    }

    private async Task<object> StockBajoAsync(string token, bool soloAgotados, CancellationToken ct)
    {
        var pagina = await stock.BajoMinimoAsync(token, soloAgotados, MaxStock, ct);
        var nombres = await NombresAsync(pagina.Items.Select(s => s.Sku), ct);
        return new
        {
            totalSkus = pagina.Total,
            mostrados = pagina.Items.Count,
            skus = pagina.Items.OrderBy(s => s.CantidadDisponible).Select(s => new
            {
                sku = s.Sku,
                producto = nombres.GetValueOrDefault(s.Sku)?.Nombre,
                opciones = DescribirOpciones(nombres.GetValueOrDefault(s.Sku)?.Opciones),
                disponible = s.CantidadDisponible,
                reservado = s.CantidadReservada,
                fisico = s.CantidadFisica,
                minimo = s.StockMinimo
            })
        };
    }

    private async Task<object> StockAsync(string token, string? sku, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return Error("Falta el SKU.");
        }
        var s = await stock.PorSkuAsync(token, sku.Trim().ToUpperInvariant(), ct);
        if (s is null)
        {
            return Error($"No existe el SKU {sku} en Inventario.");
        }
        var movimientos = await stock.MovimientosAsync(token, s.Sku, MaxMovimientos, ct);
        var nombre = (await NombresAsync([s.Sku], ct)).GetValueOrDefault(s.Sku);
        return new
        {
            sku = s.Sku,
            producto = nombre?.Nombre,
            opciones = DescribirOpciones(nombre?.Opciones),
            fisico = s.CantidadFisica,
            reservado = s.CantidadReservada,
            disponible = s.CantidadDisponible,
            minimo = s.StockMinimo,
            bajoMinimo = s.CantidadDisponible <= s.StockMinimo,
            ultimosMovimientos = movimientos.Select(m => new
            {
                fecha = Fecha(m.CreatedAt),
                tipo = m.Tipo,
                cantidad = m.Cantidad,
                fisicoResultante = m.FisicaResultante,
                motivo = m.Motivo
            })
        };
    }

    private async Task<object> ProponerAvanzarAsync(string? numero, string? estado, SesionHerramientas sesion, CancellationToken ct)
    {
        if (sesion.Acciones.Count >= SesionHerramientas.MaxAcciones)
        {
            return Error("Ya hay varias acciones por confirmar; pide que las confirmen primero.");
        }
        if (!PideAvanzar.IsMatch(Normalizar(sesion.TextoDelCliente)))
        {
            return Error("El usuario no pidió cambiar el estado de un pedido: no propongas esta acción.");
        }
        if (string.IsNullOrWhiteSpace(numero))
        {
            return Error("Falta el número del pedido.");
        }

        var p = await pedidos.PorNumeroAsync(sesion.Usuario.AccessToken, numero, ct);
        if (p is null)
        {
            return Error($"No hay un pedido {numero} visible para este usuario.");
        }

        var destino = CodigoEstado(estado);
        if (!SiguienteEstado.TryGetValue(p.Estado, out var siguiente))
        {
            return Error($"El pedido {p.Numero} está «{Estado(p.Estado)}» y no tiene un siguiente estado de despacho.");
        }
        if (destino != siguiente)
        {
            return Error($"El pedido {p.Numero} está «{Estado(p.Estado)}»: solo puede pasar a «{Estado(siguiente)}».");
        }

        var descripcion = $"Marcar el pedido {p.Numero} como «{Estado(siguiente)}»";
        sesion.Acciones.Add(new AccionPropuesta(AccionPropuesta.AvanzarPedido, descripcion,
            PedidoId: p.PedidoId, Numero: p.Numero, EstadoNuevo: siguiente));
        return new
        {
            propuestaCreada = true,
            descripcion,
            nota = "Todavía NO se cambió: el usuario debe pulsar «Confirmar» debajo del mensaje."
        };
    }

    private async Task<object> BuscarProductosAsync(string? texto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return Error("Falta el texto a buscar.");
        }
        var encontrados = await productos.BuscarAsync(texto, FiltroPrecio.Extraer(texto), ct);
        return new
        {
            productos = encontrados.Select(c => new
            {
                nombre = c.Nombre,
                slug = c.Slug,
                marca = c.Marca,
                categoria = c.Categoria,
                precio = Pesos(c.Precio),
                colores = c.Colores,
                tallas = c.Tallas,
                disponible = c.Disponible
            }),
            nota = encontrados.Count == 0 ? "Ningún producto activo se parece a esa búsqueda." : null
        };
    }

    /// <summary>Nombre de producto por SKU. Si Catálogo no responde, se devuelve vacío y el stock se muestra igual.</summary>
    private async Task<Dictionary<string, VarianteResumenCatalogo>> NombresAsync(IEnumerable<string> skus, CancellationToken ct)
    {
        try
        {
            return await catalogo.VariantesAsync(skus, ct);
        }
        catch (Exception ex) when (Fallos.EsTransitorio(ex) && !ct.IsCancellationRequested)
        {
            logger.LogWarning("No se pudieron leer los nombres de los SKU: {Mensaje}", ex.Message);
            return [];
        }
    }

    /// <summary>"En preparación", "enpreparacion" o "EnPreparacion" → "EnPreparacion".</summary>
    private static string? CodigoEstado(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado))
        {
            return null;
        }
        var buscado = Normalizar(estado).Replace(" ", "");
        return EstadosPedido
            .Where(e => Normalizar(e.Key) == buscado || Normalizar(e.Value).Replace(" ", "") == buscado)
            .Select(e => e.Key)
            .FirstOrDefault();
    }

    private static bool Booleano(JsonElement argumentos, string clave) =>
        argumentos.ValueKind == JsonValueKind.Object && argumentos.TryGetProperty(clave, out var valor) && valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => bool.TryParse(valor.GetString(), out var b) && b,
            _ => false
        };
}
