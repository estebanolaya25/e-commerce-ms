namespace CapaDto.Respuestas
{
    public class MovimientoDto
    {
        public long MovimientoId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public int FisicaResultante { get; set; }
        public int ReservadaResultante { get; set; }
        public Guid? ReferenciaId { get; set; }
        public string? Motivo { get; set; }
        public Guid? UsuarioId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
