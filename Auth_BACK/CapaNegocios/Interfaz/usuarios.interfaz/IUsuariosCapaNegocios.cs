using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.usuarios.interfaz
{
    public interface IUsuariosCapaNegocios
    {
        Task<PaginaDto<UsuarioResumenDto>> Listar(FiltroUsuariosRequest filtro);
        Task<UsuarioDetalleDto?> Obtener(Guid userId);
        Task<List<RolDto>> ListarRoles();

        Task<ResultadoAuth<UsuarioDetalleDto>> Crear(CrearUsuarioRequest request, ContextoAdministrador admin);
        Task<ResultadoAuth<UsuarioDetalleDto>> CambiarRoles(Guid userId, CambiarRolesRequest request, ContextoAdministrador admin);
        Task<ResultadoAuth<UsuarioDetalleDto>> CambiarEstado(Guid userId, bool activo, ContextoAdministrador admin);
        Task<ResultadoAuth<UsuarioDetalleDto>> Desbloquear(Guid userId, ContextoAdministrador admin);
    }
}
