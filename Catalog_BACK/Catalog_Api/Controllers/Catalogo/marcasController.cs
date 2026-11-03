using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.marcas.interfaz;
using Catalog_Api.seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog_Api.Controllers.Catalogo
{
    [Route("api/catalogo/marcas")]
    [ApiController]
    public class marcasController : ControllerBase
    {
        private readonly IMarcasCapaNegocios _marcasCapaNegocios;

        public marcasController(IMarcasCapaNegocios marcasCapaNegocios)
        {
            _marcasCapaNegocios = marcasCapaNegocios;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Listar([FromQuery] bool incluirInactivas = false)
        {
            if (incluirInactivas && !Roles.PuedeGestionarCatalogo(User))
                return Forbid();

            List<MarcaDto> marcas = await _marcasCapaNegocios.Listar(incluirInactivas);
            return Ok(marcas);
        }

        [HttpPost]
        [Authorize(Roles = Roles.GestionCatalogo)]
        public async Task<IActionResult> Crear(MarcaRequest request)
        {
            ResultadoCatalogo<MarcaDto> resultado = await _marcasCapaNegocios.Crear(request);
            return resultado.Exitoso
                ? StatusCode(StatusCodes.Status201Created, resultado.Datos)
                : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPut]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("{marcaId:guid}")]
        public async Task<IActionResult> Actualizar(Guid marcaId, MarcaRequest request)
        {
            ResultadoCatalogo<MarcaDto> resultado = await _marcasCapaNegocios.Actualizar(marcaId, request);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPatch]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("{marcaId:guid}/estado")]
        public async Task<IActionResult> CambiarEstado(Guid marcaId, CambiarEstadoRequest request)
        {
            ResultadoCatalogo<bool> resultado = await _marcasCapaNegocios.CambiarEstado(marcaId, request.activo!.Value);
            return resultado.Exitoso ? NoContent() : RespuestaCatalogo.MapearError(this, resultado);
        }
    }
}
