using CapaDatos.Interfaz.pedidos.interfaz;
using CapaDatos.Interfaz.servicios.interfaz;
using CapaDto.Externos;
using CapaDto.Modelos;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.pedidos.interfaz;
using Ecommerce.Contratos.Pedidos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CapaNegocios.Implementacion.pedidos.Implementacion
{
    public class clsPedidosCapaNegocios : IPedidosCapaNegocios
    {
        private static readonly HashSet<string> EstadosDespachables = new()
        {
            EstadoPedido.Pagado, EstadoPedido.EnPreparacion, EstadoPedido.Enviado, EstadoPedido.Entregado
        };

        private static readonly Dictionary<string, string> SiguienteEstado = new()
        {
            [EstadoPedido.Pagado] = EstadoPedido.EnPreparacion,
            [EstadoPedido.EnPreparacion] = EstadoPedido.Enviado,
            [EstadoPedido.Enviado] = EstadoPedido.Entregado
        };

        private static readonly Dictionary<string, string> MotivoPorDefecto = new()
        {
            [EstadoPedido.EnPreparacion] = "El pedido está en preparación",
            [EstadoPedido.Enviado] = "El pedido fue despachado",
            [EstadoPedido.Entregado] = "El pedido fue entregado"
        };

        private readonly IPedidosCapaDatos _pedidosCapaDatos;
        private readonly ICarritoCliente _carritoCliente;
        private readonly IInventarioCliente _inventarioCliente;
        private readonly IPagosCliente _pagosCliente;
        private readonly IConfiguration _configuration;
        private readonly ILogger<clsPedidosCapaNegocios> _logger;

        public clsPedidosCapaNegocios(
            IPedidosCapaDatos pedidosCapaDatos,
            ICarritoCliente carritoCliente,
            IInventarioCliente inventarioCliente,
            IPagosCliente pagosCliente,
            IConfiguration configuration,
            ILogger<clsPedidosCapaNegocios> logger)
        {
            _pedidosCapaDatos = pedidosCapaDatos;
            _carritoCliente = carritoCliente;
            _inventarioCliente = inventarioCliente;
            _pagosCliente = pagosCliente;
            _configuration = configuration;
            _logger = logger;
        }

        private decimal UmbralEnvioGratis => decimal.TryParse(_configuration["Pedidos:EnvioGratisDesde"], out decimal valor) ? valor : 150000m;
        private decimal CostoEnvio => decimal.TryParse(_configuration["Pedidos:CostoEnvio"], out decimal valor) ? valor : 12000m;
        private int MinutosParaPagar => int.TryParse(_configuration["Pedidos:MinutosParaPagar"], out int valor) ? valor : 15;

        public async Task<ResultadoPedido<PedidoDto>> Checkout(Guid usuarioId, string? usuarioEmail, string autorizacionUsuario, string? idempotencyKey, CheckoutRequest request)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 64)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.DatosInvalidos, "Envíe la cabecera Idempotency-Key (máximo 64 caracteres) para evitar pedidos duplicados.");

            // 1. Si ya se procesó este mismo intento (doble clic o reintento de red), se devuelve el mismo pedido.
            PedidoDto? existente = await _pedidosCapaDatos.ObtenerPorIdempotencia(usuarioId, idempotencyKey);
            if (existente != null)
                return ResultadoPedido<PedidoDto>.Ok(existente, yaExistia: true);

            // 2. El carrito trae los precios actuales de Catálogo y el stock de Inventario: el cliente nunca envía precios.
            CarritoExternoDto? carrito = await _carritoCliente.ObtenerCarrito(autorizacionUsuario);
            if (carrito == null)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.ServicioNoDisponible, "No se pudo leer el carrito. Intente de nuevo.");
            if (carrito.Items.Count == 0)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.DatosInvalidos, "El carrito está vacío.");
            if (!carrito.InformacionCompleta)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.ServicioNoDisponible, "No se pudieron confirmar precios o stock en este momento. Intente de nuevo.");
            if (!carrito.PuedeComprar)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.Conflicto, "Hay productos no disponibles en el carrito; ajústelos antes de comprar.");

            List<ItemPedidoDto> items = carrito.Items.Select(i => new ItemPedidoDto
            {
                Sku = i.Sku,
                Nombre = i.Nombre ?? i.Sku,
                Opciones = i.Opciones.Count == 0 ? null : JsonSerializer.Serialize(i.Opciones),
                ImagenUrl = i.ImagenUrl,
                PrecioUnitario = i.PrecioUnitario!.Value,
                Cantidad = i.Cantidad,
                Subtotal = i.PrecioUnitario!.Value * i.Cantidad
            }).ToList();

            decimal subtotal = items.Sum(i => i.Subtotal);
            decimal envio = subtotal >= UmbralEnvioGratis ? 0 : CostoEnvio;

            // 3. Reserva todo-o-nada en Inventario. El id del pedido se deriva de la clave de idempotencia,
            //    así un reintento reutiliza la misma reserva en lugar de apartar stock dos veces.
            Guid pedidoId = IdDeterministico(usuarioId, idempotencyKey);
            ResultadoReservaExterna reserva = await _inventarioCliente.Reservar(pedidoId, items.Select(i => (i.Sku, i.Cantidad)));

            if (reserva.Estado == EstadoLlamada.NoDisponible)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.ServicioNoDisponible, "Inventario no está disponible. Intente de nuevo.");
            if (reserva.Estado == EstadoLlamada.Conflicto)
                return ResultadoPedido<PedidoDto>.SinStock(reserva.Faltantes);
            if (reserva.Estado != EstadoLlamada.Exitosa)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.DatosInvalidos, "Inventario rechazó la reserva.");
            if (reserva.EstadoReserva != "Activa")
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.Conflicto, "Este intento de compra ya no es válido; vuelva a intentarlo desde el carrito.");

            // 4. Se guarda el pedido y su evento PedidoCreado en la misma transacción (Outbox).
            try
            {
                long consecutivo = await _pedidosCapaDatos.ObtenerSiguienteNumero();
                var nuevo = new NuevoPedido
                {
                    PedidoId = pedidoId,
                    Numero = $"PED-{DateTime.UtcNow:yyyy}-{consecutivo:D6}",
                    UsuarioId = usuarioId,
                    UsuarioEmail = usuarioEmail,
                    IdempotencyKey = idempotencyKey,
                    Subtotal = subtotal,
                    CostoEnvio = envio,
                    Total = subtotal + envio,
                    ExpiraEn = DateTime.UtcNow.AddMinutes(MinutosParaPagar),
                    Items = items,
                    Direccion = new DireccionDto
                    {
                        NombreDestinatario = request.direccion.nombreDestinatario.Trim(),
                        Telefono = request.direccion.telefono.Trim(),
                        Direccion = request.direccion.direccion.Trim(),
                        Ciudad = request.direccion.ciudad.Trim(),
                        Departamento = request.direccion.departamento.Trim(),
                        Notas = string.IsNullOrWhiteSpace(request.direccion.notas) ? null : request.direccion.notas.Trim()
                    }
                };

                var evento = EventoOutbox.Crear(new PedidoCreado
                {
                    PedidoId = nuevo.PedidoId,
                    Numero = nuevo.Numero,
                    UsuarioId = usuarioId,
                    UsuarioEmail = usuarioEmail,
                    Total = nuevo.Total,
                    Items = items.Select(i => new ItemPedidoCreado { Sku = i.Sku, Cantidad = i.Cantidad }).ToList(),
                    CreadoEn = DateTime.UtcNow
                });

                if (!await _pedidosCapaDatos.CrearPedido(nuevo, evento))
                    return ResultadoPedido<PedidoDto>.Ok((await _pedidosCapaDatos.ObtenerPorIdempotencia(usuarioId, idempotencyKey))!, yaExistia: true);

                _logger.LogInformation("Pedido {Numero} creado por {Total:C0}.", nuevo.Numero, nuevo.Total);
                return ResultadoPedido<PedidoDto>.Ok((await _pedidosCapaDatos.ObtenerPedido(pedidoId))!);
            }
            catch (Exception ex)
            {
                // Compensación: si el pedido no se pudo guardar, se devuelve el stock reservado.
                _logger.LogError(ex, "No se pudo guardar el pedido {PedidoId}; se libera la reserva.", pedidoId);
                await _inventarioCliente.Liberar(pedidoId);
                throw;
            }
        }

        public async Task<ResultadoPedido<PagoIniciadoDto>> Pagar(Guid usuarioId, Guid pedidoId, PagarRequest request)
        {
            PedidoDto? pedido = await ObtenerDelUsuario(usuarioId, pedidoId);
            if (pedido == null)
                return ResultadoPedido<PagoIniciadoDto>.Error(EstadoResultado.NoEncontrado, "Pedido no encontrado.");
            if (pedido.Estado != EstadoPedido.PendientePago)
                return ResultadoPedido<PagoIniciadoDto>.Error(EstadoResultado.Conflicto, $"El pedido no admite pagos en estado {pedido.Estado}.");
            if (pedido.ExpiraEn <= DateTime.UtcNow)
                return ResultadoPedido<PagoIniciadoDto>.Error(EstadoResultado.Conflicto, "El tiempo para pagar este pedido venció.");

            // Marca el pedido "PagoEnProceso" antes de llamar a Pagos: un doble clic no inicia dos cobros.
            ResultadoTransicion marcado = await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
            {
                PedidoId = pedidoId,
                EstadosPermitidos = new[] { EstadoPedido.PendientePago },
                NuevoEstado = EstadoPedido.PagoEnProceso,
                Motivo = $"Pago iniciado con tarjeta terminada en {Ultimos4(request.tarjeta.numero)}",
                UsuarioId = usuarioId,
                ActualizarErrorPago = true,
                ErrorPago = null
            });
            if (marcado != ResultadoTransicion.Aplicada)
                return ResultadoPedido<PagoIniciadoDto>.Error(EstadoResultado.Conflicto, "Ya hay un pago en proceso para este pedido.");

            ResultadoPagoExterno pago = await _pagosCliente.IniciarPago(pedidoId, pedido.Total, request.tarjeta);
            if (pago.Estado != EstadoLlamada.Exitosa || pago.Pago == null)
            {
                string motivo = pago.Mensaje ?? "Pagos no está disponible.";
                await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
                {
                    PedidoId = pedidoId,
                    EstadosPermitidos = new[] { EstadoPedido.PagoEnProceso },
                    NuevoEstado = EstadoPedido.PendientePago,
                    Motivo = $"No se pudo iniciar el pago: {motivo}",
                    ActualizarErrorPago = true,
                    ErrorPago = motivo
                });

                return pago.Estado == EstadoLlamada.DatosInvalidos
                    ? ResultadoPedido<PagoIniciadoDto>.Error(EstadoResultado.DatosInvalidos, motivo)
                    : ResultadoPedido<PagoIniciadoDto>.Error(EstadoResultado.ServicioNoDisponible, "No se pudo iniciar el pago. Intente de nuevo.");
            }

            await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
            {
                PedidoId = pedidoId,
                EstadosPermitidos = new[] { EstadoPedido.PagoEnProceso },
                PagoId = pago.Pago.PagoId,
                Motivo = $"Pago {pago.Pago.PagoId} recibido por la pasarela ({pago.Pago.Marca} ****{pago.Pago.Ultimos4})"
            });

            return ResultadoPedido<PagoIniciadoDto>.Ok(new PagoIniciadoDto
            {
                PedidoId = pedidoId,
                PagoId = pago.Pago.PagoId,
                EstadoPedido = EstadoPedido.PagoEnProceso,
                Mensaje = "Pago en proceso. Consulte el pedido en unos segundos para ver el resultado."
            });
        }

        public async Task<ResultadoPedido<PedidoDto>> Cancelar(Guid usuarioId, Guid pedidoId)
        {
            PedidoDto? pedido = await ObtenerDelUsuario(usuarioId, pedidoId);
            if (pedido == null)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.NoEncontrado, "Pedido no encontrado.");
            if (pedido.Estado == EstadoPedido.PagoEnProceso)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.Conflicto, "Hay un pago en proceso; espere el resultado.");
            if (pedido.Estado != EstadoPedido.PendientePago)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.Conflicto, $"Un pedido en estado {pedido.Estado} no se puede cancelar.");

            EstadoLlamada liberacion = await _inventarioCliente.Liberar(pedidoId);
            if (liberacion == EstadoLlamada.NoDisponible)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.ServicioNoDisponible, "No se pudo liberar el stock en este momento. Intente de nuevo.");

            ResultadoTransicion resultado = await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
            {
                PedidoId = pedidoId,
                EstadosPermitidos = new[] { EstadoPedido.PendientePago },
                NuevoEstado = EstadoPedido.Cancelado,
                Motivo = "Cancelado por el cliente; stock liberado",
                UsuarioId = usuarioId,
                Evento = EventoOutbox.Crear(new PedidoCancelado
                {
                    PedidoId = pedidoId,
                    Numero = pedido.Numero,
                    UsuarioId = pedido.UsuarioId,
                    UsuarioEmail = pedido.UsuarioEmail,
                    Motivo = "Cancelado por el cliente",
                    CanceladoEn = DateTime.UtcNow
                })
            });

            if (resultado != ResultadoTransicion.Aplicada)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.Conflicto, "El pedido cambió de estado; recárguelo.");

            return ResultadoPedido<PedidoDto>.Ok((await _pedidosCapaDatos.ObtenerPedido(pedidoId))!);
        }

        public async Task<PedidoDto?> ObtenerDelUsuario(Guid usuarioId, Guid pedidoId)
        {
            PedidoDto? pedido = await _pedidosCapaDatos.ObtenerPedido(pedidoId);
            return pedido?.UsuarioId == usuarioId ? pedido : null;
        }

        public Task<PedidoDto?> Obtener(Guid pedidoId) => _pedidosCapaDatos.ObtenerPedido(pedidoId);

        public async Task<PaginaDto<PedidoResumenDto>> Listar(Guid? usuarioId, FiltroPedidosRequest filtro)
        {
            var (items, total) = await _pedidosCapaDatos.ListarPedidos(usuarioId, filtro);
            return new PaginaDto<PedidoResumenDto> { Items = items, Pagina = filtro.pagina, Tamano = filtro.tamano, Total = total };
        }

        // Bodega solo trabaja pedidos ya pagados: los pendientes, cancelados o expirados no existen para ella.
        public async Task<PaginaDto<PedidoResumenDto>> ListarParaBodega(FiltroPedidosRequest filtro)
        {
            if (filtro.estado != null && !EstadosDespachables.Contains(filtro.estado))
                return new PaginaDto<PedidoResumenDto> { Pagina = filtro.pagina, Tamano = filtro.tamano };

            var (items, total) = await _pedidosCapaDatos.ListarPedidos(null, filtro, soloDespachables: true);
            return new PaginaDto<PedidoResumenDto> { Items = items, Pagina = filtro.pagina, Tamano = filtro.tamano, Total = total };
        }

        // Para despachar basta nombre, teléfono y dirección: el correo del cliente no se expone a bodega.
        public async Task<PedidoDto?> ObtenerParaBodega(Guid pedidoId)
        {
            PedidoDto? pedido = await _pedidosCapaDatos.ObtenerPedido(pedidoId);
            if (pedido == null || !EstadosDespachables.Contains(pedido.Estado))
                return null;

            pedido.UsuarioEmail = null;
            return pedido;
        }

        public async Task<ResultadoPedido<PedidoDto>> AvanzarEstado(Guid pedidoId, CambiarEstadoPedidoRequest request, Guid? usuarioId)
        {
            string? anterior = SiguienteEstado.FirstOrDefault(par => par.Value == request.estado).Key;
            if (anterior == null)
                return ResultadoPedido<PedidoDto>.Error(EstadoResultado.DatosInvalidos, "Solo se puede avanzar a EnPreparacion, Enviado o Entregado.");

            ResultadoTransicion resultado = await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
            {
                PedidoId = pedidoId,
                EstadosPermitidos = new[] { anterior },
                NuevoEstado = request.estado,
                Motivo = string.IsNullOrWhiteSpace(request.motivo) ? MotivoPorDefecto[request.estado] : request.motivo.Trim(),
                UsuarioId = usuarioId
            });

            if (resultado != ResultadoTransicion.Aplicada)
            {
                PedidoDto? actual = await _pedidosCapaDatos.ObtenerPedido(pedidoId);
                return actual == null
                    ? ResultadoPedido<PedidoDto>.Error(EstadoResultado.NoEncontrado, "Pedido no encontrado.")
                    : ResultadoPedido<PedidoDto>.Error(EstadoResultado.Conflicto, $"No se puede pasar de {actual.Estado} a {request.estado}; debe estar en {anterior}.");
            }

            return ResultadoPedido<PedidoDto>.Ok((await _pedidosCapaDatos.ObtenerPedido(pedidoId))!);
        }

        private static Guid IdDeterministico(Guid usuarioId, string idempotencyKey)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{usuarioId:N}:{idempotencyKey}"));
            return new Guid(hash.AsSpan(0, 16));
        }

        private static string Ultimos4(string numero)
        {
            string digitos = new(numero.Where(char.IsDigit).ToArray());
            return digitos.Length >= 4 ? digitos[^4..] : digitos;
        }
    }
}
