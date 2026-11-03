using CapaDatos.Interfaz.reservas.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.reservas.interfaz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CapaNegocios.Implementacion.reservas.Implementacion
{
    public class clsReservasCapaNegocios : IReservasCapaNegocios
    {
        private const string MensajeNoEncontrada = "No existe una reserva para ese pedido.";

        private readonly IReservasCapaDatos _reservasCapaDatos;
        private readonly IConfiguration _configuration;
        private readonly ILogger<clsReservasCapaNegocios> _logger;

        public clsReservasCapaNegocios(IReservasCapaDatos reservasCapaDatos, IConfiguration configuration, ILogger<clsReservasCapaNegocios> logger)
        {
            _reservasCapaDatos = reservasCapaDatos;
            _configuration = configuration;
            _logger = logger;
        }

        private int MinutosReserva => int.TryParse(_configuration["Inventario:MinutosReserva"], out int valor) ? valor : 15;

        public Task<ReservaDto?> ObtenerReserva(Guid pedidoId) => _reservasCapaDatos.ObtenerReserva(pedidoId);

        public async Task<(ResultadoInventario<ReservaDto> Resultado, bool YaExistia)> Reservar(ReservarRequest request)
        {
            List<ItemReservaRequest> items = request.items
                .Select(i => new ItemReservaRequest { sku = i.sku.Trim().ToUpperInvariant(), cantidad = i.cantidad })
                .ToList();

            if (items.Select(i => i.sku).Distinct().Count() != items.Count)
                return (ResultadoInventario<ReservaDto>.Error(EstadoResultado.DatosInvalidos, "Un SKU no puede repetirse en la misma reserva; sume las cantidades."), false);

            var (reserva, faltantes, yaExistia) = await _reservasCapaDatos.CrearReserva(
                request.pedidoId, items, DateTime.UtcNow.AddMinutes(MinutosReserva));

            if (yaExistia)
                return (ResultadoInventario<ReservaDto>.Ok(reserva!), true);

            if (faltantes.Count > 0)
                return (ResultadoInventario<ReservaDto>.SinStock(faltantes), false);

            _logger.LogInformation("Reserva creada para el pedido {PedidoId} con {Items} productos.", request.pedidoId, items.Count);
            return (ResultadoInventario<ReservaDto>.Ok(reserva!), false);
        }

        public async Task<ResultadoInventario<ReservaDto>> Confirmar(Guid pedidoId)
        {
            ResultadoCierreReserva cierre = await _reservasCapaDatos.CerrarReserva(pedidoId, true, "Confirmada", "Venta");

            if (!cierre.Encontrada)
                return ResultadoInventario<ReservaDto>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrada);

            if (cierre.EstadoFinal == "Confirmada")
                return ResultadoInventario<ReservaDto>.Ok((await _reservasCapaDatos.ObtenerReserva(pedidoId))!);

            string mensaje = cierre.EstadoFinal == "Expirada"
                ? "La reserva venció antes de confirmarse; las unidades ya se liberaron."
                : "La reserva fue liberada y ya no puede confirmarse.";
            return ResultadoInventario<ReservaDto>.Error(EstadoResultado.Conflicto, mensaje);
        }

        public async Task<ResultadoInventario<ReservaDto>> Liberar(Guid pedidoId)
        {
            ResultadoCierreReserva cierre = await _reservasCapaDatos.CerrarReserva(pedidoId, false, "Liberada", "Liberacion");

            if (!cierre.Encontrada)
                return ResultadoInventario<ReservaDto>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrada);

            if (cierre.EstadoFinal == "Confirmada")
                return ResultadoInventario<ReservaDto>.Error(EstadoResultado.Conflicto, "La reserva ya se confirmó como venta; no puede liberarse.");

            return ResultadoInventario<ReservaDto>.Ok((await _reservasCapaDatos.ObtenerReserva(pedidoId))!);
        }

        public async Task<int> ExpirarVencidas()
        {
            int total = 0;
            List<Guid> pedidos;
            do
            {
                pedidos = await _reservasCapaDatos.ObtenerPedidosConReservaVencida(50);
                foreach (Guid pedidoId in pedidos)
                {
                    ResultadoCierreReserva cierre = await _reservasCapaDatos.CerrarReserva(pedidoId, false, "Expirada", "Expiracion");
                    if (cierre.EstadoAnterior == "Activa")
                        total++;
                }
            } while (pedidos.Count == 50);

            if (total > 0)
                _logger.LogInformation("Se liberaron {Total} reservas vencidas.", total);

            return total;
        }
    }
}
