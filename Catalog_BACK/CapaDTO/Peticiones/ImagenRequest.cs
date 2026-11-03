namespace CapaDto.Peticiones
{
    public class ImagenRequest
    {
        public Stream Contenido { get; set; } = Stream.Null;
        public long Tamano { get; set; }
        public string NombreOriginal { get; set; } = string.Empty;
        public Guid? VarianteId { get; set; }
        public string? TextoAlternativo { get; set; }
        public bool EsPrincipal { get; set; }
    }
}
