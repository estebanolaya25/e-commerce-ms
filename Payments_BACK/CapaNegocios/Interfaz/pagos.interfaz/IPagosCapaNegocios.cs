using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.pagos.interfaz
{
    public interface IPagosCapaNegocios
    {
        Task<ResultadoPago<PagoDto>> IniciarPago(IniciarPagoRequest request);
        Task<PagoDto?> ObtenerPago(Guid pagoId);
        Task<PaginaDto<PagoDto>> ListarPagos(FiltroPagosRequest filtro);
        Task<ResultadoPago<PagoDto>> Reembolsar(Guid pagoId, string motivo);
        Task<int> ProcesarPendientes();
    }
}
