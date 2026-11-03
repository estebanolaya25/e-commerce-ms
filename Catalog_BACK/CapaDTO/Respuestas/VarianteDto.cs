namespace CapaDto.Respuestas
{
    public class VarianteDto
    {
        public Guid Id { get; set; }
        public Guid ProductoId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public Dictionary<string, string> Opciones { get; set; } = new();
        public decimal? PrecioPropio { get; set; }
        public decimal Precio { get; set; }
        public decimal? PrecioAntes { get; set; }
        public bool Activa { get; set; }
    }
}
