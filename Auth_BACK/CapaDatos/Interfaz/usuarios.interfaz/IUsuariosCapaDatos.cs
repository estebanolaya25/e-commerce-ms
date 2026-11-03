using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.usuarios.interfaz
{
    public interface IUsuariosCapaDatos
    {
        Task<(List<UsuarioResumenDto> Items, int Total)> ListarUsuarios(FiltroUsuariosRequest filtro);
        Task<UsuarioDetalleDto?> ObtenerUsuario(Guid userId);
        Task<List<EventoAuditoriaDto>> ListarActividad(Guid userId, int limite);
        Task<List<RolDto>> ListarRoles();
        Task<int> ContarAdministradoresActivos();

        Task<Guid?> CrearUsuario(string nombre, string apellido, string email, string emailNormalizado, string passwordHash, IReadOnlyList<string> rolesNormalizados);
        Task<bool> ReemplazarRoles(Guid userId, IReadOnlyList<string> rolesNormalizados);
        Task<bool> CambiarEstado(Guid userId, bool activo);
    }
}
