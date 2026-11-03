namespace CapaDatos.Interfaz.auditoria.interfaz
{
    public interface IAuditoriaCapaDatos
    {
        Task RegistrarEvento(Guid? userId, string tipoEvento, bool exitoso, string? ip, string? userAgent, string? detalle);
    }
}
