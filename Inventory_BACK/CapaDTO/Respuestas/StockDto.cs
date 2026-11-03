namespace CapaDto.Respuestas
{
    public class StockDto
    {
        public string Sku { get; set; } = string.Empty;
        public Guid? ProductoId { get; set; }
        public Guid? VarianteId { get; set; }
        public int CantidadFisica { get; set; }
        public int CantidadReservada { get; set; }
        public int CantidadDisponible { get; set; }
        public int StockMinimo { get; set; }
        public bool BajoMinimo => CantidadDisponible <= StockMinimo;
        public DateTime UpdatedAt { get; set; }
    }
}
