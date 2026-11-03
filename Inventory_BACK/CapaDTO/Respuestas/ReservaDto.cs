namespace CapaDto.Respuestas
{
    public class ReservaDto
    {
        public Guid ReservaId { get; set; }
        public Guid PedidoId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime ExpiraEn { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ItemReservaDto> Items { get; set; } = new();
    }

    public class ItemReservaDto
    {
        public string Sku { get; set; } = string.Empty;
        public int Cantidad { get; set; }
    }

    public class FaltanteDto
    {
        public string Sku { get; set; } = string.Empty;
        public int Solicitado { get; set; }
        public int Disponible { get; set; }
    }

    public class ResultadoCierreReserva
    {
        public bool Encontrada { get; set; }
        public string EstadoAnterior { get; set; } = string.Empty;
        public string EstadoFinal { get; set; } = string.Empty;
    }
}
