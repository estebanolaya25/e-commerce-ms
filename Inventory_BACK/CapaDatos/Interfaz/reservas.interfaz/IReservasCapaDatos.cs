using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.reservas.interfaz
{
    public interface IReservasCapaDatos
    {
        Task<ReservaDto?> ObtenerReserva(Guid pedidoId);
        Task<(ReservaDto? Reserva, List<FaltanteDto> Faltantes, bool YaExistia)> CrearReserva(Guid pedidoId, IReadOnlyList<ItemReservaRequest> items, DateTime expiraEn);
        Task<ResultadoCierreReserva> CerrarReserva(Guid pedidoId, bool vender, string estadoFinal, string tipoMovimiento);
        Task<List<Guid>> ObtenerPedidosConReservaVencida(int limite);
    }
}
