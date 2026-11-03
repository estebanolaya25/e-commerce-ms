using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.stock.interfaz;
using Inventory_Api.seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Inventory_Api.Controllers.Inventario
{
    [Route("api/inventario")]
    [ApiController]
    public class inventarioController : ControllerBase
    {
        private readonly IStockCapaNegocios _stockCapaNegocios;

        public inventarioController(IStockCapaNegocios stockCapaNegocios)
        {
            _stockCapaNegocios = stockCapaNegocios;
        }

        private Guid? UsuarioId => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id) ? id : null;

        [HttpGet]
        [AllowAnonymous]
        [Route("disponibilidad")]
        public async Task<IActionResult> Disponibilidad([FromQuery] string? skus)
        {
            ResultadoInventario<List<DisponibilidadDto>> resultado = await _stockCapaNegocios.ConsultarDisponibilidad(skus);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaInventario.MapearError(this, resultado);
        }

        [HttpGet]
        [Authorize(Roles = Roles.GestionInventario)]
        [Route("stock")]
        public async Task<IActionResult> ListarStock([FromQuery] FiltroStockRequest filtro)
        {
            return Ok(await _stockCapaNegocios.ListarStock(filtro));
        }

        [HttpGet]
        [Authorize(Roles = Roles.GestionInventario)]
        [Route("stock/{sku}")]
        public async Task<IActionResult> ObtenerStock(string sku)
        {
            StockDto? stock = await _stockCapaNegocios.ObtenerStock(sku);
            return stock == null ? NotFound(new { mensaje = "El SKU no tiene registro de inventario." }) : Ok(stock);
        }

        [HttpGet]
        [Authorize(Roles = Roles.GestionInventario)]
        [Route("stock/{sku}/movimientos")]
        public async Task<IActionResult> Movimientos(string sku, [FromQuery] int limite = 50)
        {
            ResultadoInventario<List<MovimientoDto>> resultado = await _stockCapaNegocios.ListarMovimientos(sku, limite);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaInventario.MapearError(this, resultado);
        }

        [HttpPost]
        [Authorize(Roles = Roles.Administrador)]
        [Route("stock")]
        public async Task<IActionResult> RegistrarSku(RegistrarSkuRequest request)
        {
            ResultadoInventario<StockDto> resultado = await _stockCapaNegocios.RegistrarSku(request, UsuarioId);
            return resultado.Exitoso
                ? StatusCode(StatusCodes.Status201Created, resultado.Datos)
                : RespuestaInventario.MapearError(this, resultado);
        }

        [HttpPost]
        [Authorize(Roles = Roles.GestionInventario)]
        [Route("stock/{sku}/entradas")]
        public async Task<IActionResult> RegistrarEntrada(string sku, MovimientoStockRequest request)
        {
            ResultadoInventario<StockDto> resultado = await _stockCapaNegocios.RegistrarEntrada(sku, request, UsuarioId);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaInventario.MapearError(this, resultado);
        }

        [HttpPost]
        [Authorize(Roles = Roles.GestionInventario)]
        [Route("stock/{sku}/ajustes")]
        public async Task<IActionResult> RegistrarAjuste(string sku, MovimientoStockRequest request)
        {
            ResultadoInventario<StockDto> resultado = await _stockCapaNegocios.RegistrarAjuste(sku, request, UsuarioId);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaInventario.MapearError(this, resultado);
        }

        [HttpPut]
        [Authorize(Roles = Roles.GestionInventario)]
        [Route("stock/{sku}/minimo")]
        public async Task<IActionResult> ActualizarMinimo(string sku, StockMinimoRequest request)
        {
            ResultadoInventario<StockDto> resultado = await _stockCapaNegocios.ActualizarStockMinimo(sku, request.stockMinimo);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaInventario.MapearError(this, resultado);
        }
    }
}
