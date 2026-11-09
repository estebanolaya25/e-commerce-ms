namespace Ecommerce.Contratos.Catalogo
{
    // El namespace y el nombre forman el contrato del mensaje en RabbitMQ (urn:message:Ecommerce.Contratos.Catalogo:ProductoModificado).
    // El Asistente tiene una copia idéntica: si se cambia aquí, debe cambiarse allá.
    //
    // Se publica cuando cambia algo que se ve en la tienda: datos del producto, su estado, sus variantes o sus imágenes.
    // No lleva los datos: quien lo consume vuelve a leer el producto por su slug (si responde 404, ya no está a la venta).
    public record ProductoModificado
    {
        public Guid ProductoId { get; init; }
        public string Slug { get; init; } = string.Empty;
        public DateTime ModificadoEn { get; init; }
    }

    // Cambió una categoría o una marca (nombre o estado): afecta a muchos productos a la vez.
    public record CatalogoModificado
    {
        public string Motivo { get; init; } = string.Empty;
        public DateTime ModificadoEn { get; init; }
    }
}
