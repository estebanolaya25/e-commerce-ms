using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class RefreshTokenRequest
    {
        [Required, MaxLength(200)]
        public string refreshToken { get; set; } = string.Empty;
    }
}
