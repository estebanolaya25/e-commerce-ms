using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.reservas.interfaz;
using Inventory_Api.seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory_Api.Controllers.Inventario
{
    [Route("internal/inventario/reservas")]
    [ApiController]
    [AllowAnonymous]
    [ApiKeyInterna]
    public class reservasController : ControllerBase
    {
        private readonly IReservasCapaNegocios _reservasCapaNegocios;

        public reservasController(IReservasCapaNegocios reservasCapaNegocios)
        {
            _reservasCapaNegocios = reservasCapaNegocios;
        }

        [HttpGet]
        [Route("{pedidoId:guid}")]
        public async Task<IActionResult> Obtener(Guid pedidoId)
        {
            ReservaDto? reserva = await _reservasCapaNegocios.ObtenerReserva(pedidoId);
            return reserva == null ? NotFound(new { mensaje = "No existe una reserva para ese pedido." }) : Ok(reserva);
        }

        [HttpPost]
        public async Task<IActionResult> Reservar(ReservarRequest request)
        {
            var (resultado, yaExistia) = await _reservasCapaNegocios.Reservar(request);
            if (!resultado.Exitoso)
                return RespuestaInventario.MapearError(this, resultado);

            return yaExistia ? Ok(resultado.Datos) : StatusCode(StatusCodes.Status201Created, resultado.Datos);
        }

        [HttpPost]
        [Route("{pedidoId:guid}/confirmar")]
        public async Task<IActionResult> Confirmar(Guid pedidoId)
        {
            ResultadoInventario<ReservaDto> resultado = await _reservasCapaNegocios.Confirmar(pedidoId);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaInventario.MapearError(this, resultado);
        }

        [HttpPost]
        [Route("{pedidoId:guid}/liberar")]
        public async Task<IActionResult> Liberar(Guid pedidoId)
        {
            ResultadoInventario<ReservaDto> resultado = await _reservasCapaNegocios.Liberar(pedidoId);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaInventario.MapearError(this, resultado);
        }
    }
}
