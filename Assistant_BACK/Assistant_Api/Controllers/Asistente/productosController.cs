using Assistant_Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assistant_Api.Controllers.Asistente
{
    [Route("api/asistente/productos")]
    [ApiController]
    public class productosController : ControllerBase
    {
        private readonly ProductosIndiceService _indice;
        private readonly ILogger<productosController> _logger;

        public productosController(ProductosIndiceService indice, ILogger<productosController> logger)
        {
            _indice = indice;
            _logger = logger;
        }

        // Búsqueda semántica sin pasar por el modelo de chat (rápida): "algo para la lluvia" → chaquetas impermeables.
        // Pública, como el catálogo; el gateway le aplica el mismo límite que al chat.
        [HttpGet]
        [AllowAnonymous]
        [Route("buscar")]
        public async Task<IActionResult> Buscar([FromQuery] string? q, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length > 200)
                return BadRequest(new { mensaje = "Escriba qué busca (máximo 200 caracteres)." });

            try
            {
                List<ProductoCandidato> candidatos = await _indice.BuscarAsync(q, FiltroPrecio.Extraer(q), ct);
                return Ok(candidatos);
            }
            catch (Exception ex) when (Fallos.EsTransitorio(ex) && !ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error buscando productos");
                return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = "La búsqueda no está disponible en este momento." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [Route("sincronizar")]
        public async Task<IActionResult> Sincronizar(CancellationToken ct)
        {
            try
            {
                return Ok(await _indice.SincronizarTodoAsync(ct));
            }
            catch (Exception ex) when (Fallos.EsTransitorio(ex) && !ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error sincronizando productos");
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { mensaje = "No fue posible sincronizar. Verifique que Catálogo, ChromaDB y Ollama estén en ejecución." });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Administrator")]
        [Route("estado")]
        public async Task<IActionResult> Estado(CancellationToken ct)
        {
            try
            {
                return Ok(new { productos = await _indice.CountAsync(ct) });
            }
            catch (Exception ex) when (Fallos.EsTransitorio(ex) && !ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error consultando ChromaDB");
                return StatusCode(StatusCodes.Status502BadGateway, new { mensaje = "No fue posible comunicarse con ChromaDB." });
            }
        }
    }
}
