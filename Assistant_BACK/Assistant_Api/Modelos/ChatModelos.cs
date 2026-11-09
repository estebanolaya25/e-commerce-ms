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
    string? Numero = null,
    string? EstadoNuevo = null)
{
    public const string AgregarAlCarrito = "agregar_carrito";
    public const string CancelarPedido = "cancelar_pedido";
    public const string AvanzarPedido = "avanzar_pedido";
}

public record ChatResponse(string Reply, List<string> Sources, List<ProductoSugerido> Productos, List<AccionPropuesta> Acciones);

/// <summary>Usuario con sesión: su token se reenvía a los servicios, que aplican sus propias reglas por rol.</summary>
public record ContextoUsuario(string AccessToken, string? Nombre, IReadOnlyList<string> Roles)
{
    public bool TieneAlgunRol(params string[] roles) => roles.Any(r => Roles.Contains(r, StringComparer.Ordinal));
}

/// <summary>Roles que emite Auth en el claim "role".</summary>
public static class RolesTienda
{
    public const string Administrador = "Administrator";
    public const string Soporte = "Support";
    public const string Bodega = "Warehouse";
    public const string Catalogo = "CatalogManager";

    /// <summary>Roles con acceso al panel interno (para [Authorize]).</summary>
    public const string Panel = Administrador + "," + Soporte + "," + Bodega + "," + Catalogo;

    public const string GestionCatalogo = Administrador + "," + Catalogo;
}

/// <summary>Datos del producto (tal como están en el formulario) para redactar su descripción.</summary>
public record DescripcionRequest(
    string? Nombre,
    string? Categoria,
    string? Marca,
    Dictionary<string, string>? Atributos,
    Dictionary<string, List<string>>? Opciones,
    string? DescripcionActual);

public record DescripcionResponse(string DescripcionCorta, string Descripcion);

public record ResultadoIndexacion(int Documentos, int Fragmentos);

public record ResultadoSincronizacion(int Indexados, int Eliminados);
