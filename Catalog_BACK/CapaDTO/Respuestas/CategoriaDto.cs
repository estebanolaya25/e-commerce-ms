namespace CapaDto.Respuestas
{
    public class CategoriaDto
    {
        public Guid Id { get; set; }
        public Guid? CategoriaPadreId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Orden { get; set; }
        public bool Activa { get; set; }
        public List<CategoriaDto> Hijas { get; set; } = new();
    }
}
