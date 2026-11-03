using CapaDto.Modelos;
using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.pagos.interfaz
{
    public interface IPagosCapaDatos
    {
        Task<PagoDto?> CrearPago(NuevoPago pago);
        Task<PagoDto?> ObtenerPago(Guid pagoId);
        Task<PagoDto?> ObtenerVigentePorPedido(Guid pedidoId);
        Task<(List<PagoDto> Items, int Total)> ListarPagos(FiltroPagosRequest filtro);
        Task<List<PagoPorProcesar>> ObtenerPorProcesar(int limite);
        Task<bool> ResolverPago(Guid pagoId, bool aprobado, string? referencia, string? motivoRechazo, EventoOutbox evento);
        Task<bool> Reembolsar(Guid pagoId, EventoOutbox evento);
    }
}
