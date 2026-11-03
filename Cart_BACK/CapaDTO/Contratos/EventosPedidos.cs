namespace Ecommerce.Contratos.Pedidos
{
    // Contratos publicados por Pedidos. Pedidos tiene el original: si se cambia allá, debe cambiarse aquí.
    public record PedidoCreado
    {
        public Guid PedidoId { get; init; }
        public string Numero { get; init; } = string.Empty;
        public Guid UsuarioId { get; init; }
        public string? UsuarioEmail { get; init; }
        public decimal Total { get; init; }
        public List<ItemPedidoCreado> Items { get; init; } = new();
        public DateTime CreadoEn { get; init; }
    }

    public record ItemPedidoCreado
    {
        public string Sku { get; init; } = string.Empty;
        public int Cantidad { get; init; }
    }

    public record PedidoPagado
    {
        public Guid PedidoId { get; init; }
        public string Numero { get; init; } = string.Empty;
        public Guid UsuarioId { get; init; }
        public string? UsuarioEmail { get; init; }
        public decimal Total { get; init; }
        public Guid PagoId { get; init; }
        public DateTime PagadoEn { get; init; }
    }

    public record PedidoCancelado
    {
        public Guid PedidoId { get; init; }
        public string Numero { get; init; } = string.Empty;
        public Guid UsuarioId { get; init; }
        public string? UsuarioEmail { get; init; }
        public string Motivo { get; init; } = string.Empty;
        public DateTime CanceladoEn { get; init; }
    }
}
