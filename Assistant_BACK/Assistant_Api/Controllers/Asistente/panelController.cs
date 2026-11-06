using Assistant_Api.Modelos;
using Assistant_Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assistant_Api.Controllers.Asistente
{
    // Asistente del panel interno (/admin). Solo para el personal; cada herramienta además se filtra por rol y llama a
    // los servicios con el token del empleado, que vuelven a validar sus permisos.
    [Route("api/asistente/panel")]
    [ApiController]
    [Authorize(Roles = RolesTienda.Panel)]
    public class panelController : ControllerBase
    {
        private readonly OllamaService _ollama;
        private readonly ILogger<panelController> _logger;

        public panelController(OllamaService ollama, ILogger<panelController> logger)
        {
            _ollama = ollama;
            _logger = logger;
        }

        [HttpPost]
        [Route("chat")]
        public Task<IActionResult> Chat(ChatRequest request, CancellationToken ct) =>
            Ejecutar(async () =>
            {
                List<MensajeChat>? historial = _ollama.NormalizarHistorial(request.Messages);
                ContextoUsuario? usuario = ContextoHttp.Usuario(HttpContext);
                if (historial == null || usuario == null)
                    return BadRequest(new { mensaje = "Debe enviar una pregunta." });
                return Ok(await _ollama.ChatPanelAsync(historial, usuario, ct));
            }, ct);

        [HttpPost]
        [Authorize(Roles = RolesTienda.GestionCatalogo)]
        [Route("descripcion")]
        public Task<IActionResult> Descripcion(DescripcionRequest request, CancellationToken ct) =>
            Ejecutar(async () =>
            {
                if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Categoria))
                    return BadRequest(new { mensaje = "Escriba el nombre y elija la categoría antes de generar la descripción." });
                return Ok(await _ollama.GenerarDescripcionAsync(request, ct));
            }, ct);

        private async Task<IActionResult> Ejecutar(Func<Task<IActionResult>> accion, CancellationToken ct)
        {
            try
            {
                return await accion();
            }
            catch (AsistenteOcupadoException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { mensaje = "El asistente está atendiendo muchas consultas. Intente de nuevo en unos segundos." });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error en el asistente del panel");
                return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = "El asistente no está disponible en este momento." });
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout,
                    new { mensaje = "El asistente tardó demasiado en responder. Intente de nuevo." });
            }
        }
    }
}
