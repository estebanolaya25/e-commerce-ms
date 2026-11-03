using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.imagenes.interfaz
{
    public interface IImagenesCapaDatos
    {
        Task<ImagenDto?> ObtenerImagen(Guid imagenId);
        Task<ImagenDto> CrearImagen(Guid productoId, Guid? varianteId, string rutaArchivo, string? textoAlternativo, bool esPrincipal);
        Task<string?> EliminarImagen(Guid imagenId);
        Task<bool> MarcarPrincipal(Guid imagenId);
    }
}
