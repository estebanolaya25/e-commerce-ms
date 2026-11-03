using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.imagenes.interfaz;
using Catalog_Api.seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog_Api.Controllers.Catalogo
{
    [Route("api/catalogo")]
    [ApiController]
    [Authorize(Roles = Roles.GestionCatalogo)]
    public class imagenesController : ControllerBase
    {
        private readonly IImagenesCapaNegocios _imagenesCapaNegocios;

        public imagenesController(IImagenesCapaNegocios imagenesCapaNegocios)
        {
            _imagenesCapaNegocios = imagenesCapaNegocios;
        }

        [HttpPost]
        [Route("productos/{productoId:guid}/imagenes")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Subir(Guid productoId, [FromForm] SubirImagenForm formulario)
        {
            await using Stream contenido = formulario.archivo.OpenReadStream();
            var request = new ImagenRequest
            {
                Contenido = contenido,
                Tamano = formulario.archivo.Length,
                NombreOriginal = formulario.archivo.FileName,
                VarianteId = formulario.varianteId,
                TextoAlternativo = formulario.textoAlternativo,
                EsPrincipal = formulario.esPrincipal
            };

            ResultadoCatalogo<ImagenDto> resultado = await _imagenesCapaNegocios.Subir(productoId, request);
            return resultado.Exitoso
                ? StatusCode(StatusCodes.Status201Created, resultado.Datos)
                : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpDelete]
        [Route("imagenes/{imagenId:guid}")]
        public async Task<IActionResult> Eliminar(Guid imagenId)
        {
            ResultadoCatalogo<bool> resultado = await _imagenesCapaNegocios.Eliminar(imagenId);
            return resultado.Exitoso ? NoContent() : RespuestaCatalogo.MapearError(this, resultado);
        }

        [HttpPatch]
        [Route("imagenes/{imagenId:guid}/principal")]
        public async Task<IActionResult> MarcarPrincipal(Guid imagenId)
        {
            ResultadoCatalogo<bool> resultado = await _imagenesCapaNegocios.MarcarPrincipal(imagenId);
            return resultado.Exitoso ? NoContent() : RespuestaCatalogo.MapearError(this, resultado);
        }
    }
}
