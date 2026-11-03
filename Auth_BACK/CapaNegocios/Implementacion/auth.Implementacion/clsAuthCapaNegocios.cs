using CapaDatos.Interfaz.auditoria.interfaz;
using CapaDatos.Interfaz.auth.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.auth.interfaz;
using CapaNegocios.util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CapaNegocios.Implementacion.auth.Implementacion
{
    public class clsAuthCapaNegocios : IAuthCapaNegocios
    {
        private const string RolPorDefecto = "CUSTOMER";
        private const string MensajeCredencialesInvalidas = "Email o contraseña incorrectos.";
        private const string MensajeTokenInvalido = "Refresh token inválido o expirado.";

        private readonly IAuthCapaDatos _authCapaDatos;
        private readonly IAuditoriaCapaDatos _auditoriaCapaDatos;
        private readonly ITokenGenerator _tokenGenerator;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly ILogger<clsAuthCapaNegocios> _logger;

        public clsAuthCapaNegocios(
            IAuthCapaDatos authCapaDatos,
            IAuditoriaCapaDatos auditoriaCapaDatos,
            ITokenGenerator tokenGenerator,
            IPasswordHasher passwordHasher,
            IConfiguration configuration,
            ILogger<clsAuthCapaNegocios> logger)
        {
            _authCapaDatos = authCapaDatos;
            _auditoriaCapaDatos = auditoriaCapaDatos;
            _tokenGenerator = tokenGenerator;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _logger = logger;
        }

        private int MaxIntentosFallidos => int.TryParse(_configuration["Seguridad:MaxIntentosFallidos"], out int valor) ? valor : 5;
        private int MinutosBloqueo => int.TryParse(_configuration["Seguridad:MinutosBloqueo"], out int valor) ? valor : 15;

        public async Task<ResultadoAuth<AuthResponseDto>> Registrar(RegistroRequest request, string? ip, string? userAgent)
        {
            string? errorPassword = ValidarPoliticaPassword(request.password);
            if (errorPassword != null)
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.DatosInvalidos, errorPassword);

            string email = request.email.Trim();
            string emailNormalizado = NormalizarEmail(email);

            if (await _authCapaDatos.ExisteEmail(emailNormalizado))
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.EmailRegistrado, "El email ya está registrado.");

            string passwordHash = _passwordHasher.Hash(request.password);
            Guid? userId = await _authCapaDatos.CrearUsuario(request.nombre.Trim(), request.apellido.Trim(), email, emailNormalizado, passwordHash, RolPorDefecto);

            if (userId == null)
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.EmailRegistrado, "El email ya está registrado.");

            await Auditar(userId, "UserRegistered", true, ip, userAgent, null);

            var usuario = new UsuarioCredencialDto
            {
                UserId = userId.Value,
                FirstName = request.nombre.Trim(),
                LastName = request.apellido.Trim(),
                Email = email,
                IsActive = true
            };

            AuthResponseDto respuesta = await EmitirTokensNuevaSesion(usuario, ip);
            return ResultadoAuth<AuthResponseDto>.Ok(respuesta);
        }

        public async Task<ResultadoAuth<AuthResponseDto>> Login(LoginRequest request, string? ip, string? userAgent)
        {
            UsuarioCredencialDto? usuario = await _authCapaDatos.ObtenerUsuarioPorEmail(NormalizarEmail(request.email));

            if (usuario == null)
            {
                _passwordHasher.Verificar(request.password, null);
                await Auditar(null, "LoginFailed", false, ip, userAgent, "Email no registrado");
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.CredencialesInvalidas, MensajeCredencialesInvalidas);
            }

            if (usuario.LockoutEnd.HasValue && usuario.LockoutEnd.Value > DateTime.UtcNow)
            {
                await Auditar(usuario.UserId, "LoginLockedOut", false, ip, userAgent, null);
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.CuentaBloqueada,
                    "La cuenta está bloqueada temporalmente por intentos fallidos. Intente más tarde.");
            }

            if (!_passwordHasher.Verificar(request.password, usuario.PasswordHash))
            {
                DateTime? bloqueoHasta = await _authCapaDatos.RegistrarIntentoFallido(usuario.UserId, MaxIntentosFallidos, MinutosBloqueo);
                bool bloqueada = bloqueoHasta.HasValue && bloqueoHasta.Value > DateTime.UtcNow;
                await Auditar(usuario.UserId, bloqueada ? "AccountLocked" : "LoginFailed", false, ip, userAgent, "Contraseña incorrecta");
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.CredencialesInvalidas, MensajeCredencialesInvalidas);
            }

            if (!usuario.IsActive)
            {
                await Auditar(usuario.UserId, "LoginInactiveUser", false, ip, userAgent, null);
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.CuentaInactiva, "La cuenta está inactiva.");
            }

            if (usuario.FailedLoginAttempts > 0 || usuario.LockoutEnd.HasValue)
                await _authCapaDatos.ReiniciarIntentosFallidos(usuario.UserId);

            AuthResponseDto respuesta = await EmitirTokensNuevaSesion(usuario, ip);
            await Auditar(usuario.UserId, "LoginSucceeded", true, ip, userAgent, null);
            return ResultadoAuth<AuthResponseDto>.Ok(respuesta);
        }

        public async Task<ResultadoAuth<AuthResponseDto>> RefrescarToken(string refreshToken, string? ip, string? userAgent)
        {
            RefreshTokenDto? actual = await _authCapaDatos.ObtenerRefreshToken(HashToken(refreshToken));

            if (actual == null)
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.TokenInvalido, MensajeTokenInvalido);

            if (actual.RevokedAt.HasValue)
            {
                if (actual.ReplacedByTokenId.HasValue)
                    await RevocarFamiliaPorReutilizacion(actual, ip, userAgent);

                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.TokenInvalido, MensajeTokenInvalido);
            }

            if (actual.ExpiresAt <= DateTime.UtcNow)
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.TokenInvalido, MensajeTokenInvalido);

            UsuarioCredencialDto? usuario = await _authCapaDatos.ObtenerUsuarioPorId(actual.UserId);
            if (usuario == null || !usuario.IsActive)
            {
                await _authCapaDatos.RevocarFamiliaTokens(actual.TokenFamilyId, ip);
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.TokenInvalido, MensajeTokenInvalido);
            }

            string nuevoRefreshToken = _tokenGenerator.GenerarRefreshToken();
            bool rotado = await _authCapaDatos.RotarRefreshToken(
                actual.RefreshTokenId,
                actual.UserId,
                HashToken(nuevoRefreshToken),
                actual.TokenFamilyId,
                _tokenGenerator.ObtenerExpiracionRefreshToken(),
                ip);

            if (!rotado)
            {
                await RevocarFamiliaPorReutilizacion(actual, ip, userAgent);
                return ResultadoAuth<AuthResponseDto>.Error(EstadoResultado.TokenInvalido, MensajeTokenInvalido);
            }

            AuthResponseDto respuesta = await ConstruirRespuesta(usuario, nuevoRefreshToken);
            await Auditar(usuario.UserId, "TokenRefreshed", true, ip, userAgent, null);
            return ResultadoAuth<AuthResponseDto>.Ok(respuesta);
        }

        public async Task<bool> RevocarToken(string refreshToken, string? ip, string? userAgent)
        {
            RefreshTokenDto? actual = await _authCapaDatos.ObtenerRefreshToken(HashToken(refreshToken));
            if (actual == null || actual.RevokedAt.HasValue)
                return false;

            bool revocado = await _authCapaDatos.RevocarRefreshToken(actual.RefreshTokenId, ip);
            if (revocado)
                await Auditar(actual.UserId, "Logout", true, ip, userAgent, null);

            return revocado;
        }

        public async Task<CurrentUserInfo?> ObtenerUsuarioActual(Guid userId)
        {
            UsuarioCredencialDto? usuario = await _authCapaDatos.ObtenerUsuarioPorId(userId);
            if (usuario == null || !usuario.IsActive)
                return null;

            return new CurrentUserInfo
            {
                IdUsuario = usuario.UserId,
                Nombre = usuario.FirstName,
                Apellido = usuario.LastName,
                Email = usuario.Email,
                EmailVerificado = usuario.IsEmailVerified,
                Roles = await _authCapaDatos.ObtenerRolesUsuario(usuario.UserId)
            };
        }

        public async Task<ResultadoAuth<bool>> CambiarPassword(Guid userId, CambiarPasswordRequest request, string? ip, string? userAgent)
        {
            UsuarioCredencialDto? usuario = await _authCapaDatos.ObtenerUsuarioPorId(userId);
            if (usuario == null || !usuario.IsActive)
                return ResultadoAuth<bool>.Error(EstadoResultado.NoEncontrado, "Usuario no encontrado.");

            if (!_passwordHasher.Verificar(request.passwordActual, usuario.PasswordHash))
            {
                await Auditar(userId, "PasswordChangeFailed", false, ip, userAgent, "Contraseña actual incorrecta");
                return ResultadoAuth<bool>.Error(EstadoResultado.CredencialesInvalidas, "La contraseña actual es incorrecta.");
            }

            if (request.passwordActual == request.passwordNueva)
                return ResultadoAuth<bool>.Error(EstadoResultado.DatosInvalidos, "La nueva contraseña debe ser diferente a la actual.");

            string? errorPassword = ValidarPoliticaPassword(request.passwordNueva);
            if (errorPassword != null)
                return ResultadoAuth<bool>.Error(EstadoResultado.DatosInvalidos, errorPassword);

            await _authCapaDatos.ActualizarPassword(userId, _passwordHasher.Hash(request.passwordNueva));
            await _authCapaDatos.RevocarTokensUsuario(userId, ip);
            await Auditar(userId, "PasswordChanged", true, ip, userAgent, null);
            return ResultadoAuth<bool>.Ok(true);
        }

        private async Task<AuthResponseDto> EmitirTokensNuevaSesion(UsuarioCredencialDto usuario, string? ip)
        {
            string refreshToken = _tokenGenerator.GenerarRefreshToken();
            await _authCapaDatos.GuardarRefreshToken(
                usuario.UserId,
                HashToken(refreshToken),
                Guid.NewGuid(),
                _tokenGenerator.ObtenerExpiracionRefreshToken(),
                ip);

            return await ConstruirRespuesta(usuario, refreshToken);
        }

        private async Task<AuthResponseDto> ConstruirRespuesta(UsuarioCredencialDto usuario, string refreshToken)
        {
            List<string> roles = await _authCapaDatos.ObtenerRolesUsuario(usuario.UserId);
            AccessTokenDto accessToken = _tokenGenerator.GenerarAccessToken(usuario.UserId, usuario.Email, usuario.FirstName, usuario.LastName, roles);

            return new AuthResponseDto
            {
                access_token = accessToken.Token,
                refresh_token = refreshToken,
                expiration = accessToken.Expiracion,
                userId = usuario.UserId,
                user = usuario.Email,
                roles = roles
            };
        }

        private async Task RevocarFamiliaPorReutilizacion(RefreshTokenDto token, string? ip, string? userAgent)
        {
            await _authCapaDatos.RevocarFamiliaTokens(token.TokenFamilyId, ip);
            await Auditar(token.UserId, "RefreshTokenReuseDetected", false, ip, userAgent, $"Familia {token.TokenFamilyId} revocada");
        }

        private async Task Auditar(Guid? userId, string tipoEvento, bool exitoso, string? ip, string? userAgent, string? detalle)
        {
            try
            {
                await _auditoriaCapaDatos.RegistrarEvento(userId, tipoEvento, exitoso, ip, userAgent, detalle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar el evento de auditoría {TipoEvento}", tipoEvento);
            }
        }

        private static string NormalizarEmail(string email) => clsPoliticaCuenta.NormalizarEmail(email);

        private static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

        private static string? ValidarPoliticaPassword(string password) => clsPoliticaCuenta.ValidarPassword(password);
    }
}
