namespace Ecommerce.Contratos.Catalogo
{
    // El namespace y el nombre forman el contrato del mensaje en RabbitMQ (urn:message:Ecommerce.Contratos.Catalogo:VarianteCreada).
    // Catálogo tiene una copia idéntica: si se cambia aquí, debe cambiarse allá.
    public record VarianteCreada
    {
        public string Sku { get; init; } = string.Empty;
        public Guid ProductoId { get; init; }
        public Guid VarianteId { get; init; }
        public DateTime CreadaEn { get; init; }
    }
}
