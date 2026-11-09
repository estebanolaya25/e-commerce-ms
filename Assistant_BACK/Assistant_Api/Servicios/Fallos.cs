namespace Assistant_Api.Servicios;

public static class Fallos
{
    /// <summary>
    /// Fallo de red o de un servicio externo (Ollama, ChromaDB, Catálogo, Inventario) que puede resolverse solo.
    /// Incluye los rechazos de Polly (circuit breaker abierto, timeout), que no heredan de HttpRequestException.
    /// </summary>
    public static bool EsTransitorio(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or Polly.ExecutionRejectedException;
}
