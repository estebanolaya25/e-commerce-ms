using CapaDto.Respuestas;
using Microsoft.AspNetCore.Mvc;

namespace Catalog_Api.Controllers.Catalogo
{
    public static class RespuestaCatalogo
    {
        public static IActionResult MapearError<T>(ControllerBase controller, ResultadoCatalogo<T> resultado)
        {
            var cuerpo = new { mensaje = resultado.Mensaje };

            return resultado.Estado switch
            {
                EstadoResultado.DatosInvalidos => controller.BadRequest(cuerpo),
                EstadoResultado.NoEncontrado => controller.NotFound(cuerpo),
                EstadoResultado.Conflicto => controller.Conflict(cuerpo),
                _ => controller.StatusCode(StatusCodes.Status500InternalServerError, cuerpo)
            };
        }
    }
}
