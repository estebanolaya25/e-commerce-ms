namespace CapaNegocios.Interfaz.imagenes.interfaz
{
    public interface IAlmacenamientoImagenes
    {
        Task<string> Guardar(byte[] contenido, string carpeta, string extension);
        Task Eliminar(string rutaArchivo);
        string ObtenerUrl(string rutaArchivo);
    }
}
