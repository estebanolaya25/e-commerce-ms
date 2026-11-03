using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class CambiarPasswordRequest
    {
        [Required, MaxLength(128)]
        public string passwordActual { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(128)]
        public string passwordNueva { get; set; } = string.Empty;
    }
}
