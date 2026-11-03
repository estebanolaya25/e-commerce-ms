using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.pedidos.interfaz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Orders_Api.Controllers.Pedidos
{
    [Route("api/pedidos")]
    [ApiController]
    [Authorize]
    public class pedidosController : ControllerBase
    {
        private const string RolAdministrador = "Administrator";
        private const string RolSoporte = "Support";
        private const string RolesConsulta = "Administrator,Support,Warehouse";
        private const string RolesDespacho = "Administrator,Warehouse";

        private readonly IPedidosCapaNegocios _pedidosCapaNegocios;

        public pedidosController(IPedidosCapaNegocios pedidosCapaNegocios)
        {
            _pedidosCapaNegocios = pedidosCapaNegocios;
        }

        // Bodega (sin ser también administrador o soporte) solo ve los pedidos pagados.
        private bool EsSoloBodega => !User.IsInRole(RolAdministrador) && !User.IsInRole(RolSoporte);

        private Guid? UsuarioId => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id) ? id : null;

        [HttpPost]
        public async Task<IActionResult> Checkout(CheckoutRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
        {
            if (UsuarioId is not Guid usuarioId)
                return Unauthorized();

            string? email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
            ResultadoPedido<PedidoDto> resultado = await _pedidosCapaNegocios.Checkout(
                usuarioId, email, Request.Headers.Authorization.ToString(), idempotencyKey, request);

            if (!resultado.Exitoso)
                return MapearError(resultado);
            return resultado.YaExistia ? Ok(resultado.Datos) : StatusCode(StatusCodes.Status201Created, resultado.Datos);
        }

        [HttpGet]
        public async Task<IActionResult> MisPedidos([FromQuery] FiltroPedidosRequest filtro)
        {
            if (UsuarioId is not Guid usuarioId)
                return Unauthorized();

            return Ok(await _pedidosCapaNegocios.Listar(usuarioId, filtro));
        }

        [HttpGet]
        [Route("{pedidoId:guid}")]
        public async Task<IActionResult> MiPedido(Guid pedidoId)
        {
            if (UsuarioId is not Guid usuarioId)
                return Unauthorized();

            PedidoDto? pedido = await _pedidosCapaNegocios.ObtenerDelUsuario(usuarioId, pedidoId);
            return pedido == null ? NotFound(new { mensaje = "Pedido no encontrado." }) : Ok(pedido);
        }

        [HttpPost]
        [Route("{pedidoId:guid}/pagar")]
        public async Task<IActionResult> Pagar(Guid pedidoId, PagarRequest request)
        {
            if (UsuarioId is not Guid usuarioId)
                return Unauthorized();

            ResultadoPedido<PagoIniciadoDto> resultado = await _pedidosCapaNegocios.Pagar(usuarioId, pedidoId, request);
            return resultado.Exitoso ? Accepted(resultado.Datos) : MapearError(resultado);
        }

        [HttpPost]
        [Route("{pedidoId:guid}/cancelar")]
        public async Task<IActionResult> Cancelar(Guid pedidoId)
        {
            if (UsuarioId is not Guid usuarioId)
                return Unauthorized();

            ResultadoPedido<PedidoDto> resultado = await _pedidosCapaNegocios.Cancelar(usuarioId, pedidoId);
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        [HttpGet]
        [Authorize(Roles = RolesConsulta)]
        [Route("admin")]
        public async Task<IActionResult> TodosLosPedidos([FromQuery] FiltroPedidosRequest filtro)
        {
            return Ok(EsSoloBodega
                ? await _pedidosCapaNegocios.ListarParaBodega(filtro)
                : await _pedidosCapaNegocios.Listar(null, filtro));
        }

        [HttpGet]
        [Authorize(Roles = RolesConsulta)]
        [Route("admin/{pedidoId:guid}")]
        public async Task<IActionResult> CualquierPedido(Guid pedidoId)
        {
            PedidoDto? pedido = EsSoloBodega
                ? await _pedidosCapaNegocios.ObtenerParaBodega(pedidoId)
                : await _pedidosCapaNegocios.Obtener(pedidoId);
            return pedido == null ? NotFound(new { mensaje = "Pedido no encontrado." }) : Ok(pedido);
        }

        [HttpPatch]
        [Authorize(Roles = RolesDespacho)]
        [Route("{pedidoId:guid}/estado")]
        public async Task<IActionResult> AvanzarEstado(Guid pedidoId, CambiarEstadoPedidoRequest request)
        {
            ResultadoPedido<PedidoDto> resultado = await _pedidosCapaNegocios.AvanzarEstado(pedidoId, request, UsuarioId);
            if (resultado.Exitoso && EsSoloBodega)
                resultado.Datos!.UsuarioEmail = null;
            return resultado.Exitoso ? Ok(resultado.Datos) : MapearError(resultado);
        }

        private IActionResult MapearError<T>(ResultadoPedido<T> resultado)
        {
            return resultado.Estado switch
            {
                EstadoResultado.DatosInvalidos => BadRequest(new { mensaje = resultado.Mensaje }),
                EstadoResultado.NoEncontrado => NotFound(new { mensaje = resultado.Mensaje }),
                EstadoResultado.Conflicto => Conflict(new { mensaje = resultado.Mensaje }),
                EstadoResultado.StockInsuficiente => Conflict(new { mensaje = resultado.Mensaje, faltantes = resultado.Faltantes }),
                EstadoResultado.ServicioNoDisponible => StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = resultado.Mensaje }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { mensaje = resultado.Mensaje })
            };
        }
    }
}
