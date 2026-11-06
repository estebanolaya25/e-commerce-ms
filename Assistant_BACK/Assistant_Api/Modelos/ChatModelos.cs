namespace Assistant_Api.Modelos;

public record MensajeChat(string Role, string Content);

/// <summary>El modelo de Ollama no se elige desde el cliente: lo fija la configuración del servicio.</summary>
public record ChatRequest(List<MensajeChat>? Messages);

/// <summary>Tarjeta de producto que el chat muestra con enlace al detalle. Disponible es null si Inventario no respondió.</summary>
public record ProductoSugerido(
    string Slug,
    string Nombre,
    string? Marca,
    decimal Precio,
    decimal? PrecioAntes,
    string? ImagenUrl,
    bool? Disponible);

public record ChatResponse(string Reply, List<string> Sources, List<ProductoSugerido> Productos);

public record ResultadoIndexacion(int Documentos, int Fragmentos);

public record ResultadoSincronizacion(int Indexados, int Eliminados);
