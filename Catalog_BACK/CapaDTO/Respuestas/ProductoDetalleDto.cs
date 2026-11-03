namespace CapaDto.Respuestas
{
    public class ProductoDetalleDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? DescripcionCorta { get; set; }
        public string? Descripcion { get; set; }
        public decimal PrecioBase { get; set; }
        public Dictionary<string, string> Atributos { get; set; } = new();
        public bool Destacado { get; set; }
        public bool Activo { get; set; }
        public Guid CategoriaId { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string CategoriaSlug { get; set; } = string.Empty;
        public Guid? MarcaId { get; set; }
        public string? Marca { get; set; }
        public string? MarcaSlug { get; set; }
        public List<VarianteDto> Variantes { get; set; } = new();
        public List<ImagenDto> Imagenes { get; set; } = new();
    }
}
