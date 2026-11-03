using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class MarcaRequest
    {
        [Required, MaxLength(100)]
        public string nombre { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? slug { get; set; }
    }
}
