using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.auth.interfaz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Auth_Api.Controllers.Auth
{
    [Route("api/[controller]")]
    [ApiController]
    public class authController : ControllerBase
    {
        private readonly IAuthCapaNegocios _authCapaNegocios;

        public authController(IAuthCapaNegocios authCapaNegocios)
        {
            _authCapaNegocios = authCapaNegocios;
        }

        private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
        private string UserAgent => Request.Headers.UserAgent.ToString();

        [HttpPost]
        [AllowAnonymous]
        [Route("registro")]
        public async Task<IActionResult> Registro(RegistroRequest request)
        {
            ResultadoAuth<AuthResponseDto> resultado = await _authCapaNegocios.Registrar(request, Ip, UserAgent);
            return resultado.Exitoso ? StatusCode(StatusCodes.Status201Created, resultado.Datos) : MapearError(resultado);
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            ResultadoAuth<AuthResponseDto> resultado = await _authCapaNegocios.Login(request, Ip, UserAgent);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("refresh")]
        public async Task<IActionResult> Refresh(RefreshTokenRequest request)
        {
            ResultadoAuth<AuthResponseDto> resultado = await _authCapaNegocios.RefrescarToken(request.refreshToken, Ip, UserAgent);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("revoke")]
        public async Task<IActionResult> Revoke(RefreshTokenRequest request)
        {
            bool revocado = await _authCapaNegocios.RevocarToken(request.refreshToken, Ip, UserAgent);
            return revocado
                ? Ok(new { mensaje = "Sesión cerrada correctamente." })
                : BadRequest(new { mensaje = "Token no encontrado o ya revocado." });
        }

        [HttpGet]
        [Authorize]
        [Route("current")]
        public async Task<IActionResult> Current()
        {
            if (!TryObtenerUserId(out Guid userId))
                return Unauthorized();

            CurrentUserInfo? usuario = await _authCapaNegocios.ObtenerUsuarioActual(userId);
            return usuario == null ? Unauthorized() : Ok(usuario);
        }

        [HttpPost]
        [Authorize]
        [Route("cambiar-password")]
        public async Task<IActionResult> CambiarPassword(CambiarPasswordRequest request)
        {
            if (!TryObtenerUserId(out Guid userId))
                return Unauthorized();

            ResultadoAuth<bool> resultado = await _authCapaNegocios.CambiarPassword(userId, request, Ip, UserAgent);
            return resultado.Exitoso
                ? Ok(new { mensaje = "Contraseña actualizada. Inicie sesión nuevamente." })
                : MapearError(resultado);
        }

        private bool TryObtenerUserId(out Guid userId)
        {
            return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out userId);
        }

        private IActionResult MapearError<T>(ResultadoAuth<T> resultado)
        {
            var cuerpo = new { mensaje = resultado.Mensaje };

            return resultado.Estado switch
            {
                EstadoResultado.DatosInvalidos => BadRequest(cuerpo),
                EstadoResultado.CredencialesInvalidas => Unauthorized(cuerpo),
                EstadoResultado.TokenInvalido => Unauthorized(cuerpo),
                EstadoResultado.CuentaInactiva => StatusCode(StatusCodes.Status403Forbidden, cuerpo),
                EstadoResultado.CuentaBloqueada => StatusCode(StatusCodes.Status423Locked, cuerpo),
                EstadoResultado.EmailRegistrado => Conflict(cuerpo),
                EstadoResultado.NoEncontrado => NotFound(cuerpo),
                _ => StatusCode(StatusCodes.Status500InternalServerError, cuerpo)
            };
        }
    }
}
