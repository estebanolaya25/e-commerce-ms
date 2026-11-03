using CapaDto.Respuestas;
using Microsoft.AspNetCore.Mvc;

namespace Inventory_Api.Controllers.Inventario
{
    public static class RespuestaInventario
    {
        public static IActionResult MapearError<T>(ControllerBase controller, ResultadoInventario<T> resultado)
        {
            return resultado.Estado switch
            {
                EstadoResultado.DatosInvalidos => controller.BadRequest(new { mensaje = resultado.Mensaje }),
                EstadoResultado.NoEncontrado => controller.NotFound(new { mensaje = resultado.Mensaje }),
                EstadoResultado.Conflicto => controller.Conflict(new { mensaje = resultado.Mensaje }),
                EstadoResultado.StockInsuficiente => controller.Conflict(new { mensaje = resultado.Mensaje, faltantes = resultado.Faltantes }),
                _ => controller.StatusCode(StatusCodes.Status500InternalServerError, new { mensaje = resultado.Mensaje })
            };
        }
    }
}
