using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class LoginRequest
    {
        [Required, EmailAddress, MaxLength(254)]
        public string email { get; set; } = string.Empty;

        [Required, MaxLength(128)]
        public string password { get; set; } = string.Empty;
    }
}
