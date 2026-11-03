using CapaDatos.Interfaz.auditoria.interfaz;
using CapaDatos.Interfaz.auth.interfaz;
using CapaDatos.Interfaz.usuarios.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.auth.interfaz;
using CapaNegocios.Interfaz.usuarios.interfaz;
using CapaNegocios.util;
using Microsoft.Extensions.Logging;

namespace CapaNegocios.Implementacion.usuarios.Implementacion
{
    public class clsUsuariosCapaNegocios : IUsuariosCapaNegocios
    {
        private const string RolAdministrador = "ADMINISTRATOR";
        private const int EventosEnDetalle = 25;
        private const string MensajeNoEncontrado = "Usuario no encontrado.";
        private const string MensajeUltimoAdministrador = "Debe quedar al menos un administrador activo.";

        private readonly IUsuariosCapaDatos _usuariosCapaDatos;
        private readonly IAuthCapaDatos _authCapaDatos;
        private readonly IAuditoriaCapaDatos _auditoriaCapaDatos;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILogger<clsUsuariosCapaNegocios> _logger;

        public clsUsuariosCapaNegocios(
            IUsuariosCapaDatos usuariosCapaDatos,
            IAuthCapaDatos authCapaDatos,
            IAuditoriaCapaDatos auditoriaCapaDatos,
            IPasswordHasher passwordHasher,
            ILogger<clsUsuariosCapaNegocios> logger)
        {
            _usuariosCapaDatos = usuariosCapaDatos;
            _authCapaDatos = authCapaDatos;
            _auditoriaCapaDatos = auditoriaCapaDatos;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public async Task<PaginaDto<UsuarioResumenDto>> Listar(FiltroUsuariosRequest filtro)
        {
            var (items, total) = await _usuariosCapaDatos.ListarUsuarios(filtro);
            return new PaginaDto<UsuarioResumenDto> { Items = items, Pagina = filtro.pagina, Tamano = filtro.tamano, Total = total };
        }

        public async Task<UsuarioDetalleDto?> Obtener(Guid userId)
        {
            UsuarioDetalleDto? usuario = await _usuariosCapaDatos.ObtenerUsuario(userId);
            if (usuario != null)
                usuario.Actividad = await _usuariosCapaDatos.ListarActividad(userId, EventosEnDetalle);
            return usuario;
        }

        public Task<List<RolDto>> ListarRoles() => _usuariosCapaDatos.ListarRoles();

        public async Task<ResultadoAuth<UsuarioDetalleDto>> Crear(CrearUsuarioRequest request, ContextoAdministrador admin)
        {
            string? errorPassword = clsPoliticaCuenta.ValidarPassword(request.password);
            if (errorPassword != null)
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.DatosInvalidos, errorPassword);

            var (roles, errorRoles) = await ValidarRoles(request.roles);
            if (errorRoles != null)
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.DatosInvalidos, errorRoles);

            string email = request.email.Trim();
            string emailNormalizado = clsPoliticaCuenta.NormalizarEmail(email);
            if (await _authCapaDatos.ExisteEmail(emailNormalizado))
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.EmailRegistrado, "El email ya está registrado.");

            Guid? userId = await _usuariosCapaDatos.CrearUsuario(
                request.nombre.Trim(), request.apellido.Trim(), email, emailNormalizado, _passwordHasher.Hash(request.password), roles);
            if (userId == null)
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.EmailRegistrado, "El email ya está registrado.");

            await Auditar(userId, "UserCreatedByAdmin", admin, $"Roles: {string.Join(", ", await NombresRoles(roles))}");
            return ResultadoAuth<UsuarioDetalleDto>.Ok((await Obtener(userId.Value))!);
        }

        public async Task<ResultadoAuth<UsuarioDetalleDto>> CambiarRoles(Guid userId, CambiarRolesRequest request, ContextoAdministrador admin)
        {
            UsuarioDetalleDto? usuario = await _usuariosCapaDatos.ObtenerUsuario(userId);
            if (usuario == null)
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrado);

            var (roles, errorRoles) = await ValidarRoles(request.roles);
            if (errorRoles != null)
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.DatosInvalidos, errorRoles);

            bool eraAdministrador = EsAdministrador(usuario.Roles);
            bool seguiraSiendolo = roles.Contains(RolAdministrador);
            if (eraAdministrador && !seguiraSiendolo)
            {
                if (userId == admin.UserId)
                    return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.OperacionNoPermitida, "No puede quitarse su propio rol de administrador.");
                if (usuario.Activo && await _usuariosCapaDatos.ContarAdministradoresActivos() <= 1)
                    return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.OperacionNoPermitida, MensajeUltimoAdministrador);
            }

            if (!await _usuariosCapaDatos.ReemplazarRoles(userId, roles))
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrado);

            // El cambio llega al usuario cuando renueva su token (el refresh vuelve a leer los roles).
            await Auditar(userId, "RolesChanged", admin,
                $"{TextoRoles(usuario.Roles)} → {string.Join(", ", await NombresRoles(roles))}");
            return ResultadoAuth<UsuarioDetalleDto>.Ok((await Obtener(userId))!);
        }

        public async Task<ResultadoAuth<UsuarioDetalleDto>> CambiarEstado(Guid userId, bool activo, ContextoAdministrador admin)
        {
            UsuarioDetalleDto? usuario = await _usuariosCapaDatos.ObtenerUsuario(userId);
            if (usuario == null)
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrado);

            if (usuario.Activo == activo)
                return ResultadoAuth<UsuarioDetalleDto>.Ok((await Obtener(userId))!);

            if (!activo)
            {
                if (userId == admin.UserId)
                    return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.OperacionNoPermitida, "No puede desactivar su propia cuenta.");
                if (EsAdministrador(usuario.Roles) && await _usuariosCapaDatos.ContarAdministradoresActivos() <= 1)
                    return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.OperacionNoPermitida, MensajeUltimoAdministrador);
            }

            if (!await _usuariosCapaDatos.CambiarEstado(userId, activo))
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrado);

            // Sin refresh tokens vivos no puede renovar la sesión: sale cuando venza su access token.
            if (!activo)
                await _authCapaDatos.RevocarTokensUsuario(userId, admin.Ip);

            await Auditar(userId, activo ? "UserActivated" : "UserDeactivated", admin, null);
            return ResultadoAuth<UsuarioDetalleDto>.Ok((await Obtener(userId))!);
        }

        public async Task<ResultadoAuth<UsuarioDetalleDto>> Desbloquear(Guid userId, ContextoAdministrador admin)
        {
            UsuarioDetalleDto? usuario = await _usuariosCapaDatos.ObtenerUsuario(userId);
            if (usuario == null)
                return ResultadoAuth<UsuarioDetalleDto>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrado);

            await _authCapaDatos.ReiniciarIntentosFallidos(userId);
            await Auditar(userId, "UserUnlocked", admin, null);
            return ResultadoAuth<UsuarioDetalleDto>.Ok((await Obtener(userId))!);
        }

        // Devuelve los roles normalizados (sin repetir) o un mensaje si alguno no existe.
        private async Task<(List<string> Roles, string? Error)> ValidarRoles(IEnumerable<string> solicitados)
        {
            List<string> roles = solicitados
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            if (roles.Count == 0)
                return (roles, "Asigne al menos un rol.");

            HashSet<string> existentes = (await _usuariosCapaDatos.ListarRoles())
                .Select(r => r.Nombre.ToUpperInvariant())
                .ToHashSet();

            string? desconocido = roles.FirstOrDefault(r => !existentes.Contains(r));
            return desconocido != null ? (roles, $"El rol {desconocido} no existe.") : (roles, null);
        }

        private async Task<List<string>> NombresRoles(IEnumerable<string> normalizados)
        {
            var nombres = (await _usuariosCapaDatos.ListarRoles()).ToDictionary(r => r.Nombre.ToUpperInvariant(), r => r.Nombre);
            return normalizados.Select(r => nombres.GetValueOrDefault(r, r)).OrderBy(r => r).ToList();
        }

        private static bool EsAdministrador(IEnumerable<string> roles) =>
            roles.Any(r => r.Equals(RolAdministrador, StringComparison.OrdinalIgnoreCase));

        private static string TextoRoles(List<string> roles) => roles.Count == 0 ? "sin roles" : string.Join(", ", roles);

        // El evento queda en la auditoría del usuario afectado, con el administrador que lo hizo.
        private async Task Auditar(Guid? userId, string tipoEvento, ContextoAdministrador admin, string? detalle)
        {
            string texto = detalle == null ? $"Por {admin.Email}" : $"{detalle}. Por {admin.Email}";
            try
            {
                await _auditoriaCapaDatos.RegistrarEvento(userId, tipoEvento, true, admin.Ip, admin.UserAgent, texto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar el evento de auditoría {TipoEvento}", tipoEvento);
            }
        }
    }
}
