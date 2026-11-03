namespace CapaDto.Respuestas
{
    public class MarcaDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool Activa { get; set; }
    }
}
