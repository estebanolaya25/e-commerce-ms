namespace Ecommerce.Contratos.Catalogo
{
    // Copia idéntica de Catalog_BACK/CapaDTO/Contratos/ProductoModificado.cs: el namespace y el nombre forman el
    // contrato del mensaje en RabbitMQ (urn:message:Ecommerce.Contratos.Catalogo:ProductoModificado).
    public record ProductoModificado
    {
        public Guid ProductoId { get; init; }
        public string Slug { get; init; } = string.Empty;
        public DateTime ModificadoEn { get; init; }
    }

    public record CatalogoModificado
    {
        public string Motivo { get; init; } = string.Empty;
        public DateTime ModificadoEn { get; init; }
    }
}
