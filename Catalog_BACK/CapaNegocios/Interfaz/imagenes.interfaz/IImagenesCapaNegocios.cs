using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.imagenes.interfaz
{
    public interface IImagenesCapaNegocios
    {
        Task<ResultadoCatalogo<ImagenDto>> Subir(Guid productoId, ImagenRequest request);
        Task<ResultadoCatalogo<bool>> Eliminar(Guid imagenId);
        Task<ResultadoCatalogo<bool>> MarcarPrincipal(Guid imagenId);
    }
}
