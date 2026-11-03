using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.productos.interfaz
{
    public interface IProductosCapaDatos
    {
        Task<(List<ProductoResumenDto> Items, int Total)> ListarProductos(FiltroProductosRequest filtro, bool incluirInactivos);
        Task<ProductoDetalleDto?> ObtenerProductoPorSlug(string slug, bool incluirInactivos);
        Task<ProductoDetalleDto?> ObtenerProductoPorId(Guid productoId, bool incluirInactivos);
        Task<Guid> CrearProducto(CrearProductoRequest request, string slug, Guid? usuarioId);
        Task<bool> ActualizarProducto(Guid productoId, ProductoRequest request, string slug, Guid? usuarioId);
        Task<bool> CambiarEstadoProducto(Guid productoId, bool activo, Guid? usuarioId);

        Task<VarianteDto?> ObtenerVariante(Guid varianteId);
        Task<List<VarianteResumenDto>> ObtenerVariantesPorSku(IReadOnlyList<string> skus);
        Task<Guid> CrearVariante(Guid productoId, VarianteRequest request);
        Task<bool> ActualizarVariante(Guid varianteId, VarianteRequest request);
        Task<bool> CambiarEstadoVariante(Guid varianteId, bool activa);
    }
}
