using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.productos.interfaz;
using Catalog_Api.seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Catalog_Api.Controllers.Catalogo
{
    [Route("api/catalogo/productos")]
    [ApiController]
    public class productosController : ControllerBase
    {
        private readonly IProductosCapaNegocios _productosCapaNegocios;

        public productosController(IProductosCapaNegocios productosCapaNegocios)
        {
            _productosCapaNegocios = productosCapaNegocios;
        }

        private Guid? UsuarioId => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out Guid id) ? id : null;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Listar([FromQuery] FiltroProductosRequest filtro, [FromQuery] bool incluirInactivos = false)
        {
            if (incluirInactivos && !Roles.PuedeGestionarCatalogo(User))
                return Forbid();

            PaginaDto<ProductoResumenDto> pagina = await _productosCapaNegocios.Listar(filtro, incluirInactivos);
            return Ok(pagina);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("{slug}")]
        public async Task<IActionResult> ObtenerPorSlug(string slug)
        {
            ProductoDetalleDto? producto = await _productosCapaNegocios.ObtenerPorSlug(slug);
            return producto == null ? NotFound(new { mensaje = "Producto no encontrado." }) : Ok(producto);
        }

        [HttpGet]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("id/{productoId:guid}")]
        public async Task<IActionResult> ObtenerPorId(Guid productoId)
        {
            ProductoDetalleDto? producto = await _productosCapaNegocios.ObtenerPorId(productoId);
            return producto == null ? NotFound(new { mensaje = "Producto no encontrado." }) : Ok(producto);
        }

        [HttpPost]
        [Authorize(Roles = Roles.GestionCatalogo)]
        public async Task<IActionResult> Crear(CrearProductoRequest request)
        {
            ResultadoCatalogo<ProductoDetalleDto> resultado = await _productosCapaNegocios.Crear(request, UsuarioId);
            return resultado.Exitoso
                ? StatusCode(StatusCodes.Status201Created, resultado.Datos)
                : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPut]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("{productoId:guid}")]
        public async Task<IActionResult> Actualizar(Guid productoId, ProductoRequest request)
        {
            ResultadoCatalogo<ProductoDetalleDto> resultado = await _productosCapaNegocios.Actualizar(productoId, request, UsuarioId);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPatch]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("{productoId:guid}/estado")]
        public async Task<IActionResult> CambiarEstado(Guid productoId, CambiarEstadoRequest request)
        {
            ResultadoCatalogo<bool> resultado = await _productosCapaNegocios.CambiarEstado(productoId, request.activo!.Value, UsuarioId);
            return resultado.Exitoso ? NoContent() : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPost]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("{productoId:guid}/variantes")]
        public async Task<IActionResult> AgregarVariante(Guid productoId, VarianteRequest request)
        {
            ResultadoCatalogo<VarianteDto> resultado = await _productosCapaNegocios.AgregarVariante(productoId, request);
            return resultado.Exitoso
                ? StatusCode(StatusCodes.Status201Created, resultado.Datos)
                : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("~/api/catalogo/variantes")]
        public async Task<IActionResult> ObtenerVariantesPorSku([FromQuery] string? skus)
        {
            ResultadoCatalogo<List<VarianteResumenDto>> resultado = await _productosCapaNegocios.ObtenerVariantesPorSku(skus);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPut]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("~/api/catalogo/variantes/{varianteId:guid}")]
        public async Task<IActionResult> ActualizarVariante(Guid varianteId, VarianteRequest request)
        {
            ResultadoCatalogo<VarianteDto> resultado = await _productosCapaNegocios.ActualizarVariante(varianteId, request);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPatch]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("~/api/catalogo/variantes/{varianteId:guid}/estado")]
        public async Task<IActionResult> CambiarEstadoVariante(Guid varianteId, CambiarEstadoRequest request)
        {
            ResultadoCatalogo<bool> resultado = await _productosCapaNegocios.CambiarEstadoVariante(varianteId, request.activo!.Value);
            return resultado.Exitoso ? NoContent() : RespuestaCatalogo.MapearError(this, resultado);
        }
    }
}
