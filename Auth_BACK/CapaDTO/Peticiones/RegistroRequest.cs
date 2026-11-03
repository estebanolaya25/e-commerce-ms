using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class RegistroRequest
    {
        [Required, MaxLength(100)]
        public string nombre { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string apellido { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(254)]
        public string email { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(128)]
        public string password { get; set; } = string.Empty;
    }
}
