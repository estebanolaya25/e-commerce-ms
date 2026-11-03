namespace CapaDto.Respuestas
{
    public class AccessTokenDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiracion { get; set; }
    }
}
