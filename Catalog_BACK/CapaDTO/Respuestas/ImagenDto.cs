namespace CapaDto.Respuestas
{
    public class ImagenDto
    {
        public Guid Id { get; set; }
        public Guid ProductoId { get; set; }
        public Guid? VarianteId { get; set; }
        public string RutaArchivo { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? TextoAlternativo { get; set; }
        public int Orden { get; set; }
        public bool EsPrincipal { get; set; }
    }
}
