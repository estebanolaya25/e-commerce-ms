using CapaDatos.Interfaz.pedidos.interfaz;
using CapaDatos.Interfaz.servicios.interfaz;
using CapaDto.Externos;
using CapaDto.Modelos;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.pedidos.interfaz;
using Ecommerce.Contratos.Pagos;
using Ecommerce.Contratos.Pedidos;
using Microsoft.Extensions.Logging;

namespace CapaNegocios.Implementacion.pedidos.Implementacion
{
    public class clsSagaPedidosCapaNegocios : ISagaPedidosCapaNegocios
    {
        private static readonly string[] EstadosEsperandoPago = { EstadoPedido.PendientePago, EstadoPedido.PagoEnProceso };
        private static readonly string[] TodosLosEstados =
        {
            EstadoPedido.PendientePago, EstadoPedido.PagoEnProceso, EstadoPedido.Pagado, EstadoPedido.EnPreparacion,
            EstadoPedido.Enviado, EstadoPedido.Entregado, EstadoPedido.Cancelado, EstadoPedido.Expirado
        };

        private readonly IPedidosCapaDatos _pedidosCapaDatos;
        private readonly IInventarioCliente _inventarioCliente;
        private readonly IPagosCliente _pagosCliente;
        private readonly ILogger<clsSagaPedidosCapaNegocios> _logger;

        public clsSagaPedidosCapaNegocios(
            IPedidosCapaDatos pedidosCapaDatos,
            IInventarioCliente inventarioCliente,
            IPagosCliente pagosCliente,
            ILogger<clsSagaPedidosCapaNegocios> logger)
        {
            _pedidosCapaDatos = pedidosCapaDatos;
            _inventarioCliente = inventarioCliente;
            _pagosCliente = pagosCliente;
            _logger = logger;
        }

        public async Task ProcesarPagoAprobado(Guid mensajeId, PagoAprobado evento)
        {
            PedidoDto? pedido = await _pedidosCapaDatos.ObtenerPedido(evento.PedidoId);
            if (pedido == null)
            {
                _logger.LogWarning("PagoAprobado para un pedido inexistente {PedidoId}; se ignora.", evento.PedidoId);
                return;
            }

            if (EstadosEsperandoPago.Contains(pedido.Estado))
            {
                // Paso de la saga: con el dinero confirmado, Inventario descuenta definitivamente las unidades.
                EstadoLlamada confirmacion = await _inventarioCliente.Confirmar(pedido.PedidoId);
                if (confirmacion == EstadoLlamada.NoDisponible)
                    throw new InvalidOperationException("Inventario no disponible; MassTransit reintentará el mensaje.");

                if (confirmacion == EstadoLlamada.Exitosa)
                {
                    await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
                    {
                        PedidoId = pedido.PedidoId,
                        EstadosPermitidos = EstadosEsperandoPago,
                        NuevoEstado = EstadoPedido.Pagado,
                        Motivo = $"Pago aprobado (referencia {evento.Referencia}); stock descontado",
                        PagoId = evento.PagoId,
                        ActualizarErrorPago = true,
                        ErrorPago = null,
                        MensajeId = mensajeId,
                        TipoMensaje = nameof(PagoAprobado),
                        Evento = EventoOutbox.Crear(new PedidoPagado
                        {
                            PedidoId = pedido.PedidoId,
                            Numero = pedido.Numero,
                            UsuarioId = pedido.UsuarioId,
                            UsuarioEmail = pedido.UsuarioEmail,
                            Total = pedido.Total,
                            PagoId = evento.PagoId,
                            PagadoEn = evento.AprobadoEn
                        })
                    });
                    return;
                }

                // Compensación: el pago llegó cuando la reserva ya había vencido. No hay stock garantizado: se reembolsa.
                await ReembolsarOFallar(evento.PagoId, "La reserva de stock venció antes de confirmarse el pago");
                await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
                {
                    PedidoId = pedido.PedidoId,
                    EstadosPermitidos = EstadosEsperandoPago,
                    NuevoEstado = EstadoPedido.Expirado,
                    Motivo = "Pago aprobado tarde: la reserva ya había vencido. Pago reembolsado.",
                    PagoId = evento.PagoId,
                    MensajeId = mensajeId,
                    TipoMensaje = nameof(PagoAprobado),
                    Evento = EventoCancelado(pedido, "Pago aprobado después de vencer la reserva; se reembolsó")
                });
                return;
            }

            if (pedido.Estado is EstadoPedido.Cancelado or EstadoPedido.Expirado)
            {
                await ReembolsarOFallar(evento.PagoId, $"El pedido ya estaba {pedido.Estado}");
                await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
                {
                    PedidoId = pedido.PedidoId,
                    EstadosPermitidos = TodosLosEstados,
                    Motivo = $"Pago aprobado sobre un pedido {pedido.Estado}; se solicitó reembolso",
                    MensajeId = mensajeId,
                    TipoMensaje = nameof(PagoAprobado)
                });
                return;
            }

            _logger.LogInformation("PagoAprobado repetido para el pedido {Numero} en estado {Estado}; se ignora.", pedido.Numero, pedido.Estado);
        }

        public async Task ProcesarPagoRechazado(Guid mensajeId, PagoRechazado evento)
        {
            // El pedido vuelve a PendientePago: el cliente puede intentar con otra tarjeta mientras no venza.
            ResultadoTransicion resultado = await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
            {
                PedidoId = evento.PedidoId,
                EstadosPermitidos = new[] { EstadoPedido.PagoEnProceso },
                NuevoEstado = EstadoPedido.PendientePago,
                Motivo = $"Pago rechazado: {evento.Motivo}",
                ActualizarErrorPago = true,
                ErrorPago = evento.Motivo,
                MensajeId = mensajeId,
                TipoMensaje = nameof(PagoRechazado)
            });

            if (resultado == ResultadoTransicion.EstadoNoPermitido)
                _logger.LogInformation("PagoRechazado para el pedido {PedidoId} que ya no espera pago; se ignora.", evento.PedidoId);
        }

        public async Task ProcesarPagoReembolsado(Guid mensajeId, PagoReembolsado evento)
        {
            await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
            {
                PedidoId = evento.PedidoId,
                EstadosPermitidos = TodosLosEstados,
                Motivo = $"Reembolso confirmado por {evento.Monto:N0}: {evento.Motivo}",
                MensajeId = mensajeId,
                TipoMensaje = nameof(PagoReembolsado)
            });
        }

        public async Task<int> ExpirarVencidos()
        {
            int total = 0;
            foreach (Guid pedidoId in await _pedidosCapaDatos.ObtenerPedidosVencidos(50, TimeSpan.FromMinutes(5)))
            {
                PedidoDto? pedido = await _pedidosCapaDatos.ObtenerPedido(pedidoId);
                if (pedido == null)
                    continue;

                if (await _inventarioCliente.Liberar(pedidoId) == EstadoLlamada.NoDisponible)
                {
                    _logger.LogWarning("Inventario no disponible; el pedido {Numero} se expirará en el próximo ciclo.", pedido.Numero);
                    continue;
                }

                ResultadoTransicion resultado = await _pedidosCapaDatos.AplicarTransicion(new TransicionPedido
                {
                    PedidoId = pedidoId,
                    EstadosPermitidos = EstadosEsperandoPago,
                    NuevoEstado = EstadoPedido.Expirado,
                    Motivo = "Venció el tiempo para pagar; stock liberado",
                    Evento = EventoCancelado(pedido, "Venció el tiempo para pagar")
                });

                if (resultado == ResultadoTransicion.Aplicada)
                    total++;
            }

            if (total > 0)
                _logger.LogInformation("Se expiraron {Total} pedidos sin pago.", total);
            return total;
        }

        private async Task ReembolsarOFallar(Guid pagoId, string motivo)
        {
            EstadoLlamada reembolso = await _pagosCliente.Reembolsar(pagoId, motivo);
            if (reembolso == EstadoLlamada.NoDisponible)
                throw new InvalidOperationException("Pagos no disponible para reembolsar; MassTransit reintentará el mensaje.");
        }

        private static EventoOutbox EventoCancelado(PedidoDto pedido, string motivo) => EventoOutbox.Crear(new PedidoCancelado
        {
            PedidoId = pedido.PedidoId,
            Numero = pedido.Numero,
            UsuarioId = pedido.UsuarioId,
            UsuarioEmail = pedido.UsuarioEmail,
            Motivo = motivo,
            CanceladoEn = DateTime.UtcNow
        });
    }
}
