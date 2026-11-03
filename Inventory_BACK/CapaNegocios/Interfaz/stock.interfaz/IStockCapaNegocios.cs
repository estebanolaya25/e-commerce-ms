using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.stock.interfaz
{
    public interface IStockCapaNegocios
    {
        Task<ResultadoInventario<List<DisponibilidadDto>>> ConsultarDisponibilidad(string? skus);
        Task<StockDto?> ObtenerStock(string sku);
        Task<PaginaDto<StockDto>> ListarStock(FiltroStockRequest filtro);
        Task<ResultadoInventario<List<MovimientoDto>>> ListarMovimientos(string sku, int limite);

        Task<ResultadoInventario<StockDto>> RegistrarSku(RegistrarSkuRequest request, Guid? usuarioId);
        Task<bool> RegistrarDesdeEvento(Guid mensajeId, string sku, Guid productoId, Guid varianteId);
        Task<ResultadoInventario<StockDto>> RegistrarEntrada(string sku, MovimientoStockRequest request, Guid? usuarioId);
        Task<ResultadoInventario<StockDto>> RegistrarAjuste(string sku, MovimientoStockRequest request, Guid? usuarioId);
        Task<ResultadoInventario<StockDto>> ActualizarStockMinimo(string sku, int stockMinimo);
    }
}
