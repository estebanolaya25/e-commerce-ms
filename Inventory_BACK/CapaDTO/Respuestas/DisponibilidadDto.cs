namespace CapaDto.Respuestas
{
    public class DisponibilidadDto
    {
        public string Sku { get; set; } = string.Empty;
        public bool Existe { get; set; }
        public int Disponible { get; set; }
        public bool Agotado => Disponible <= 0;
        public bool PocasUnidades { get; set; }
    }
}
