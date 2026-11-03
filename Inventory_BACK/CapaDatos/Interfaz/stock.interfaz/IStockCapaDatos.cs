using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.stock.interfaz
{
    public interface IStockCapaDatos
    {
        Task<List<DisponibilidadDto>> ObtenerDisponibilidad(IReadOnlyList<string> skus);
        Task<StockDto?> ObtenerStock(string sku);
        Task<(List<StockDto> Items, int Total)> ListarStock(FiltroStockRequest filtro);
        Task<List<MovimientoDto>> ListarMovimientos(string sku, int limite);

        Task<bool> RegistrarSku(string sku, Guid? productoId, Guid? varianteId, Guid? usuarioId, string motivo);
        Task<bool> RegistrarSkuDesdeEvento(Guid mensajeId, string tipoMensaje, string sku, Guid productoId, Guid varianteId);
        Task<bool> AplicarMovimientoFisico(string sku, int cantidad, string tipo, string motivo, Guid? usuarioId);
        Task<bool> ActualizarStockMinimo(string sku, int stockMinimo);
    }
}
