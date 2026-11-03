using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.usuarios.interfaz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Auth_Api.Controllers.Usuarios
{
    // Gestión de usuarios y roles: exclusiva del administrador.
    [Route("api/auth")]
    [ApiController]
    [Authorize(Roles = RolAdministrador)]
    public class usuariosController : ControllerBase
    {
        private const string RolAdministrador = "Administrator";

        private readonly IUsuariosCapaNegocios _usuariosCapaNegocios;

        public usuariosController(IUsuariosCapaNegocios usuariosCapaNegocios)
        {
            _usuariosCapaNegocios = usuariosCapaNegocios;
        }

        private ContextoAdministrador Administrador => new(
            Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
            User.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "administrador",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        [HttpGet]
        [Route("usuarios")]
        public async Task<IActionResult> Listar([FromQuery] FiltroUsuariosRequest filtro)
        {
            return Ok(await _usuariosCapaNegocios.Listar(filtro));
        }

        [HttpGet]
        [Route("usuarios/{userId:guid}")]
        public async Task<IActionResult> Obtener(Guid userId)
        {
            UsuarioDetalleDto? usuario = await _usuariosCapaNegocios.Obtener(userId);
            return usuario == null ? NotFound(new { mensaje = "Usuario no encontrado." }) : Ok(usuario);
        }

        [HttpGet]
        [Route("roles")]
        public async Task<IActionResult> Roles()
        {
            return Ok(await _usuariosCapaNegocios.ListarRoles());
        }

        [HttpPost]
        [Route("usuarios")]
        public async Task<IActionResult> Crear(CrearUsuarioRequest request)
        {
            ResultadoAuth<UsuarioDetalleDto> resultado = await _usuariosCapaNegocios.Crear(request, Administrador);
            return resultado.Exitoso ? StatusCode(StatusCodes.Status201Created, resultado.Datos) : MapearError(resultado);
        }

        [HttpPut]
        [Route("usuarios/{userId:guid}/roles")]
        public async Task<IActionResult> CambiarRoles(Guid userId, CambiarRolesRequest request)
        {
            ResultadoAuth<UsuarioDetalleDto> resultado = await _usuariosCapaNegocios.CambiarRoles(userId, request, Administrador);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        [HttpPatch]
        [Route("usuarios/{userId:guid}/estado")]
        public async Task<IActionResult> CambiarEstado(Guid userId, CambiarEstadoUsuarioRequest request)
        {
            ResultadoAuth<UsuarioDetalleDto> resultado = await _usuariosCapaNegocios.CambiarEstado(userId, request.activo!.Value, Administrador);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        [HttpPost]
        [Route("usuarios/{userId:guid}/desbloquear")]
        public async Task<IActionResult> Desbloquear(Guid userId)
        {
            ResultadoAuth<UsuarioDetalleDto> resultado = await _usuariosCapaNegocios.Desbloquear(userId, Administrador);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        private IActionResult MapearError<T>(ResultadoAuth<T> resultado)
        {
            var cuerpo = new { mensaje = resultado.Mensaje };

            return resultado.Estado switch
            {
                EstadoResultado.DatosInvalidos => BadRequest(cuerpo),
                EstadoResultado.EmailRegistrado => Conflict(cuerpo),
                EstadoResultado.OperacionNoPermitida => Conflict(cuerpo),
                EstadoResultado.NoEncontrado => NotFound(cuerpo),
                _ => StatusCode(StatusCodes.Status500InternalServerError, cuerpo)
            };
        }
    }
}
