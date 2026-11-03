using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.productos.interfaz
{
    public interface IProductosCapaNegocios
    {
        Task<PaginaDto<ProductoResumenDto>> Listar(FiltroProductosRequest filtro, bool incluirInactivos);
        Task<ProductoDetalleDto?> ObtenerPorSlug(string slug);
        Task<ProductoDetalleDto?> ObtenerPorId(Guid productoId);
        Task<ResultadoCatalogo<ProductoDetalleDto>> Crear(CrearProductoRequest request, Guid? usuarioId);
        Task<ResultadoCatalogo<ProductoDetalleDto>> Actualizar(Guid productoId, ProductoRequest request, Guid? usuarioId);
        Task<ResultadoCatalogo<bool>> CambiarEstado(Guid productoId, bool activo, Guid? usuarioId);

        Task<ResultadoCatalogo<List<VarianteResumenDto>>> ObtenerVariantesPorSku(string? skus);
        Task<ResultadoCatalogo<VarianteDto>> AgregarVariante(Guid productoId, VarianteRequest request);
        Task<ResultadoCatalogo<VarianteDto>> ActualizarVariante(Guid varianteId, VarianteRequest request);
        Task<ResultadoCatalogo<bool>> CambiarEstadoVariante(Guid varianteId, bool activa);
    }
}
