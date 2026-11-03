namespace Ecommerce.Contratos.Pagos
{
    // Contratos publicados por Pagos. Pagos tiene una copia idéntica: si se cambia aquí, debe cambiarse allá.
    public record PagoAprobado
    {
        public Guid PagoId { get; init; }
        public Guid PedidoId { get; init; }
        public decimal Monto { get; init; }
        public string Referencia { get; init; } = string.Empty;
        public DateTime AprobadoEn { get; init; }
    }

    public record PagoRechazado
    {
        public Guid PagoId { get; init; }
        public Guid PedidoId { get; init; }
        public string Motivo { get; init; } = string.Empty;
        public DateTime RechazadoEn { get; init; }
    }

    public record PagoReembolsado
    {
        public Guid PagoId { get; init; }
        public Guid PedidoId { get; init; }
        public decimal Monto { get; init; }
        public string Motivo { get; init; } = string.Empty;
        public DateTime ReembolsadoEn { get; init; }
    }
}
