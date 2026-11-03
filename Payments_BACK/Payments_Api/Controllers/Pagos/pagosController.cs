using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.pagos.interfaz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payments_Api.seguridad;

namespace Payments_Api.Controllers.Pagos
{
    [ApiController]
    public class pagosController : ControllerBase
    {
        private readonly IPagosCapaNegocios _pagosCapaNegocios;

        public pagosController(IPagosCapaNegocios pagosCapaNegocios)
        {
            _pagosCapaNegocios = pagosCapaNegocios;
        }

        // ---- Internos: solo Pedidos (no pasan por el gateway y exigen X-Api-Key) ----

        [HttpPost]
        [AllowAnonymous]
        [ApiKeyInterna]
        [Route("internal/pagos")]
        public async Task<IActionResult> IniciarPago(IniciarPagoRequest request)
        {
            ResultadoPago<PagoDto> resultado = await _pagosCapaNegocios.IniciarPago(request);
            if (!resultado.Exitoso)
                return MapearError(resultado);
            return resultado.YaExistia ? Ok(resultado.Datos) : Accepted(resultado.Datos);
        }

        [HttpGet]
        [AllowAnonymous]
        [ApiKeyInterna]
        [Route("internal/pagos/{pagoId:guid}")]
        public async Task<IActionResult> ObtenerInterno(Guid pagoId)
        {
            PagoDto? pago = await _pagosCapaNegocios.ObtenerPago(pagoId);
            return pago == null ? NotFound(new { mensaje = "Pago no encontrado." }) : Ok(pago);
        }

        [HttpPost]
        [AllowAnonymous]
        [ApiKeyInterna]
        [Route("internal/pagos/{pagoId:guid}/reembolsar")]
        public async Task<IActionResult> Reembolsar(Guid pagoId, ReembolsarRequest request)
        {
            ResultadoPago<PagoDto> resultado = await _pagosCapaNegocios.Reembolsar(pagoId, request.motivo);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        // ---- Consulta para administración (pasa por el gateway con JWT) ----

        [HttpGet]
        [Authorize(Roles = "Administrator,Support")]
        [Route("api/pagos")]
        public async Task<IActionResult> Listar([FromQuery] FiltroPagosRequest filtro)
        {
            return Ok(await _pagosCapaNegocios.ListarPagos(filtro));
        }

        private IActionResult MapearError<T>(ResultadoPago<T> resultado)
        {
            var cuerpo = new { mensaje = resultado.Mensaje };
            return resultado.Estado switch
            {
                EstadoResultado.DatosInvalidos => BadRequest(cuerpo),
                EstadoResultado.NoEncontrado => NotFound(cuerpo),
                EstadoResultado.Conflicto => Conflict(cuerpo),
                _ => StatusCode(StatusCodes.Status500InternalServerError, cuerpo)
            };
        }
    }
}
