using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.categorias.interfaz
{
    public interface ICategoriasCapaDatos
    {
        Task<List<CategoriaDto>> ListarCategorias(bool incluirInactivas);
        Task<CategoriaDto?> ObtenerCategoriaPorId(Guid categoriaId);
        Task<Guid> CrearCategoria(CategoriaRequest request, string slug);
        Task<bool> ActualizarCategoria(Guid categoriaId, CategoriaRequest request, string slug);
        Task<bool> CambiarEstadoCategoria(Guid categoriaId, bool activa);
        Task<bool> EsDescendiente(Guid categoriaId, Guid posibleDescendienteId);
    }
}
