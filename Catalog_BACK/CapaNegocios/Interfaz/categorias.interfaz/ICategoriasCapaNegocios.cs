using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.categorias.interfaz
{
    public interface ICategoriasCapaNegocios
    {
        Task<List<CategoriaDto>> ObtenerArbol(bool incluirInactivas);
        Task<ResultadoCatalogo<CategoriaDto>> Crear(CategoriaRequest request);
        Task<ResultadoCatalogo<CategoriaDto>> Actualizar(Guid categoriaId, CategoriaRequest request);
        Task<ResultadoCatalogo<bool>> CambiarEstado(Guid categoriaId, bool activa);
    }
}
