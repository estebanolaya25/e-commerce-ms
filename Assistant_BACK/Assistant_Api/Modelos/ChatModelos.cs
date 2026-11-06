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

/// <summary>
/// Acción que el asistente propone y el cliente debe confirmar con un botón. El asistente nunca la ejecuta: al confirmar,
/// el frontend llama a la API de siempre (Carrito o Pedidos) con la sesión del cliente.
/// </summary>
public record AccionPropuesta(
    string Tipo,
    string Descripcion,
    string? Sku = null,
    int? Cantidad = null,
    string? Slug = null,
    Guid? PedidoId = null,
    string? Numero = null)
{
    public const string AgregarAlCarrito = "agregar_carrito";
    public const string CancelarPedido = "cancelar_pedido";
}

public record ChatResponse(string Reply, List<string> Sources, List<ProductoSugerido> Productos, List<AccionPropuesta> Acciones);

/// <summary>Cliente con sesión: su token se reenvía a Pedidos y Carrito.</summary>
public record ContextoUsuario(string AccessToken, string? Nombre);

public record ResultadoIndexacion(int Documentos, int Fragmentos);

public record ResultadoSincronizacion(int Indexados, int Eliminados);
