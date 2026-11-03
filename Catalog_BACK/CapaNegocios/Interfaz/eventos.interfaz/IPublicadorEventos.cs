namespace CapaNegocios.Interfaz.eventos.interfaz
{
    public interface IPublicadorEventos
    {
        Task Publicar<T>(T evento) where T : class;
    }
}
