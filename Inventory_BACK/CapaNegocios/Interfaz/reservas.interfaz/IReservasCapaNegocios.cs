using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.reservas.interfaz
{
    public interface IReservasCapaNegocios
    {
        Task<ReservaDto?> ObtenerReserva(Guid pedidoId);
        Task<(ResultadoInventario<ReservaDto> Resultado, bool YaExistia)> Reservar(ReservarRequest request);
        Task<ResultadoInventario<ReservaDto>> Confirmar(Guid pedidoId);
        Task<ResultadoInventario<ReservaDto>> Liberar(Guid pedidoId);
        Task<int> ExpirarVencidas();
    }
}
