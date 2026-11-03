namespace CapaDto.Respuestas
{
    public class ProductoResumenDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? DescripcionCorta { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string CategoriaSlug { get; set; } = string.Empty;
        public string? Marca { get; set; }
        public string? MarcaSlug { get; set; }
        public decimal PrecioDesde { get; set; }
        public decimal? PrecioAntes { get; set; }
        public string? ImagenRuta { get; set; }
        public string? ImagenUrl { get; set; }
        public string? ImagenTextoAlternativo { get; set; }
        public bool Destacado { get; set; }
        public bool Activo { get; set; }
    }
}
