using Assistant_Api.Modelos;
using Assistant_Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assistant_Api.Controllers.Asistente
{
    // Reindexar borra y vuelve a crear la colección: solo el administrador.
    [Route("api/asistente/documentos")]
    [ApiController]
    [Authorize(Roles = "Administrator")]
    public class documentosController : ControllerBase
    {
        private readonly KnowledgeBaseService _knowledgeBase;
        private readonly ILogger<documentosController> _logger;

        public documentosController(KnowledgeBaseService knowledgeBase, ILogger<documentosController> logger)
        {
            _knowledgeBase = knowledgeBase;
            _logger = logger;
        }

        [HttpPost]
        [Route("reindexar")]
        public async Task<IActionResult> Reindexar(CancellationToken ct)
        {
            try
            {
                ResultadoIndexacion resultado = await _knowledgeBase.ReindexAsync(ct);
                return Ok(resultado);
            }
            catch (DirectoryNotFoundException ex)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { mensaje = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error indexando documentos");
                return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = "No fue posible indexar. Verifique que ChromaDB y Ollama estén en ejecución." });
            }
        }

        [HttpGet]
        [Route("estado")]
        public async Task<IActionResult> Estado(CancellationToken ct)
        {
            try
            {
                return Ok(new { fragmentos = await _knowledgeBase.CountAsync(ct) });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error consultando ChromaDB");
                return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = "No fue posible comunicarse con ChromaDB." });
            }
        }
    }
}
