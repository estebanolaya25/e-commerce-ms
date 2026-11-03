using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.pedidos.interfaz
{
    public interface IPedidosCapaNegocios
    {
        Task<ResultadoPedido<PedidoDto>> Checkout(Guid usuarioId, string? usuarioEmail, string autorizacionUsuario, string? idempotencyKey, CheckoutRequest request);
        Task<ResultadoPedido<PagoIniciadoDto>> Pagar(Guid usuarioId, Guid pedidoId, PagarRequest request);
        Task<ResultadoPedido<PedidoDto>> Cancelar(Guid usuarioId, Guid pedidoId);
        Task<PedidoDto?> ObtenerDelUsuario(Guid usuarioId, Guid pedidoId);
        Task<PedidoDto?> Obtener(Guid pedidoId);
        Task<PaginaDto<PedidoResumenDto>> Listar(Guid? usuarioId, FiltroPedidosRequest filtro);
        Task<PaginaDto<PedidoResumenDto>> ListarParaBodega(FiltroPedidosRequest filtro);
        Task<PedidoDto?> ObtenerParaBodega(Guid pedidoId);
        Task<ResultadoPedido<PedidoDto>> AvanzarEstado(Guid pedidoId, CambiarEstadoPedidoRequest request, Guid? usuarioId);
    }
}
