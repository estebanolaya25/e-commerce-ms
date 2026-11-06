namespace Assistant_Api.Modelos;

public record MensajeChat(string Role, string Content);

/// <summary>El modelo de Ollama no se elige desde el cliente: lo fija la configuración del servicio.</summary>
public record ChatRequest(List<MensajeChat>? Messages);

public record ChatResponse(string Reply, List<string> Sources);

public record ResultadoIndexacion(int Documentos, int Fragmentos);
