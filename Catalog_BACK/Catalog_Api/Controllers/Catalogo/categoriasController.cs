using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.categorias.interfaz;
using Catalog_Api.seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog_Api.Controllers.Catalogo
{
    [Route("api/catalogo/categorias")]
    [ApiController]
    public class categoriasController : ControllerBase
    {
        private readonly ICategoriasCapaNegocios _categoriasCapaNegocios;

        public categoriasController(ICategoriasCapaNegocios categoriasCapaNegocios)
        {
            _categoriasCapaNegocios = categoriasCapaNegocios;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Listar([FromQuery] bool incluirInactivas = false)
        {
            if (incluirInactivas && !Roles.PuedeGestionarCatalogo(User))
                return Forbid();

            List<CategoriaDto> arbol = await _categoriasCapaNegocios.ObtenerArbol(incluirInactivas);
            return Ok(arbol);
        }

        [HttpPost]
        [Authorize(Roles = Roles.GestionCatalogo)]
        public async Task<IActionResult> Crear(CategoriaRequest request)
        {
            ResultadoCatalogo<CategoriaDto> resultado = await _categoriasCapaNegocios.Crear(request);
            return resultado.Exitoso
                ? StatusCode(StatusCodes.Status201Created, resultado.Datos)
                : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPut]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("{categoriaId:guid}")]
        public async Task<IActionResult> Actualizar(Guid categoriaId, CategoriaRequest request)
        {
            ResultadoCatalogo<CategoriaDto> resultado = await _categoriasCapaNegocios.Actualizar(categoriaId, request);
            return resultado.Exitoso ? Ok(resultado.Datos) : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPatch]
        [Authorize(Roles = Roles.GestionCatalogo)]
        [Route("{categoriaId:guid}/estado")]
        public async Task<IActionResult> CambiarEstado(Guid categoriaId, CambiarEstadoRequest request)
        {
            ResultadoCatalogo<bool> resultado = await _categoriasCapaNegocios.CambiarEstado(categoriaId, request.activo!.Value);
            return resultado.Exitoso ? NoContent() : RespuestaCatalogo.MapearError(this, resultado);
        }
    }
}
