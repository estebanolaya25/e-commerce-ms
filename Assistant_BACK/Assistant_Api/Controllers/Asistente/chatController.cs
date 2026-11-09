using Assistant_Api.Modelos;
using Assistant_Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assistant_Api.Controllers.Asistente
{
    // Abierto sin sesión: responde preguntas generales y busca productos. Con sesión (JWT válido) el modelo puede además
    // consultar los pedidos y el carrito del cliente, siempre con el token del propio cliente.
    // El gateway le aplica un límite de peticiones propio porque cada respuesta usa la GPU varios segundos.
    [Route("api/asistente/chat")]
    [ApiController]
    [AllowAnonymous]
    public class chatController : ControllerBase
    {
        private readonly OllamaService _ollama;
        private readonly ILogger<chatController> _logger;

        public chatController(OllamaService ollama, ILogger<chatController> logger)
        {
            _ollama = ollama;
            _logger = logger;
        }

        // Igual que Preguntar, pero transmite la respuesta como Server-Sent Events mientras el modelo la genera.
        [HttpPost]
        [Route("stream")]
        public async Task PreguntarTransmitido(ChatRequest request, CancellationToken ct)
        {
            List<MensajeChat>? historial = _ollama.NormalizarHistorial(request.Messages);
            if (historial == null)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                await Response.WriteAsJsonAsync(new { mensaje = "Debe enviar una pregunta." }, ct);
                return;
            }

            ContextoUsuario? usuario = ContextoHttp.Usuario(HttpContext);
            await TransmisionSse.TransmitirAsync(Response, emitir => _ollama.ChatAsync(historial, usuario, ct, emitir), _logger, ct);
        }

        [HttpPost]
        public async Task<IActionResult> Preguntar(ChatRequest request, CancellationToken ct)
        {
            List<MensajeChat>? historial = _ollama.NormalizarHistorial(request.Messages);
            if (historial == null)
                return StatusCode(StatusCodes.Status400BadRequest, new { mensaje = "Debe enviar una pregunta." });

            try
            {
                return Ok(await _ollama.ChatAsync(historial, ContextoHttp.Usuario(HttpContext), ct));
            }
            catch (AsistenteOcupadoException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensaje = "El asistente está atendiendo muchas consultas. Intente de nuevo en unos segundos." });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error comunicándose con Ollama o ChromaDB");
                return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = "El asistente no está disponible en este momento." });
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Venció el timeout del HttpClient (no fue el cliente quien canceló).
                _logger.LogWarning("Ollama no respondió a tiempo");
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { mensaje = "El asistente tardó demasiado en responder. Intente de nuevo." });
            }
        }
    }
}
