namespace CapaDto.Respuestas
{
    public class VarianteResumenDto
    {
        public Guid VarianteId { get; set; }
        public Guid ProductoId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public Dictionary<string, string> Opciones { get; set; } = new();
        public decimal Precio { get; set; }
        public decimal? PrecioAntes { get; set; }
        public string? ImagenRuta { get; set; }
        public string? ImagenUrl { get; set; }
        public bool Disponible { get; set; }
    }
}
