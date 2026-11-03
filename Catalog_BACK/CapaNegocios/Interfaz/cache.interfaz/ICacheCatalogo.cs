namespace CapaNegocios.Interfaz.cache.interfaz
{
    public interface ICacheCatalogo
    {
        Task<T?> ObtenerOCrear<T>(string clave, TimeSpan duracion, Func<Task<T?>> consultar) where T : class;
        Task Invalidar();
    }
}
