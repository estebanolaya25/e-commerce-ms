namespace CapaDto.Respuestas
{
    public class AuthResponseDto
    {
        public string access_token { get; set; } = string.Empty;
        public string refresh_token { get; set; } = string.Empty;
        public DateTime expiration { get; set; }
        public Guid userId { get; set; }
        public string user { get; set; } = string.Empty;
        public List<string> roles { get; set; } = new();
    }
}
