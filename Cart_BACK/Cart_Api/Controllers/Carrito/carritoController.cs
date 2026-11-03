using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.carrito.interfaz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Cart_Api.Controllers.Carrito
{
    [Route("api/carrito")]
    [ApiController]
    [Authorize]
    public class carritoController : ControllerBase
    {
        private readonly ICarritoCapaNegocios _carritoCapaNegocios;

        public carritoController(ICarritoCapaNegocios carritoCapaNegocios)
        {
            _carritoCapaNegocios = carritoCapaNegocios;
        }

        [HttpGet]
        public async Task<IActionResult> Obtener()
        {
            if (!TryObtenerUsuarioId(out Guid usuarioId))
                return Unauthorized();

            return Ok(await _carritoCapaNegocios.Obtener(usuarioId));
        }

        [HttpPost]
        [Route("items")]
        public async Task<IActionResult> Agregar(AgregarItemRequest request)
        {
            if (!TryObtenerUsuarioId(out Guid usuarioId))
                return Unauthorized();

            ResultadoCarrito<CarritoDto> resultado = await _carritoCapaNegocios.Agregar(usuarioId, request);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        [HttpPut]
        [Route("items/{sku}")]
        public async Task<IActionResult> ActualizarCantidad(string sku, ActualizarCantidadRequest request)
        {
            if (!TryObtenerUsuarioId(out Guid usuarioId))
                return Unauthorized();

            ResultadoCarrito<CarritoDto> resultado = await _carritoCapaNegocios.ActualizarCantidad(usuarioId, sku, request.cantidad);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        [HttpDelete]
        [Route("items/{sku}")]
        public async Task<IActionResult> Quitar(string sku)
        {
            if (!TryObtenerUsuarioId(out Guid usuarioId))
                return Unauthorized();

            ResultadoCarrito<CarritoDto> resultado = await _carritoCapaNegocios.Quitar(usuarioId, sku);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        [HttpDelete]
        public async Task<IActionResult> Vaciar()
        {
            if (!TryObtenerUsuarioId(out Guid usuarioId))
                return Unauthorized();

            await _carritoCapaNegocios.Vaciar(usuarioId);
            return NoContent();
        }

        private bool TryObtenerUsuarioId(out Guid usuarioId)
        {
            return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out usuarioId);
        }

        private IActionResult MapearError<T>(ResultadoCarrito<T> resultado)
        {
            var cuerpo = new { mensaje = resultado.Mensaje };

            return resultado.Estado switch
            {
                EstadoResultado.DatosInvalidos => BadRequest(cuerpo),
                EstadoResultado.NoEncontrado => NotFound(cuerpo),
                EstadoResultado.Conflicto => Conflict(cuerpo),
                EstadoResultado.ServicioNoDisponible => StatusCode(StatusCodes.Status503ServiceUnavailable, cuerpo),
                _ => StatusCode(StatusCodes.Status500InternalServerError, cuerpo)
            };
        }
    }
}
