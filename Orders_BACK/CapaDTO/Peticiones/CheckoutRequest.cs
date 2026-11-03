using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class CheckoutRequest
    {
        [Required]
        public DireccionEnvioRequest direccion { get; set; } = new();
    }

    public class DireccionEnvioRequest
    {
        [Required, MinLength(3), MaxLength(150)]
        public string nombreDestinatario { get; set; } = string.Empty;

        [Required, RegularExpression(@"^\+?[0-9 ]{7,20}$", ErrorMessage = "Teléfono inválido.")]
        public string telefono { get; set; } = string.Empty;

        [Required, MinLength(5), MaxLength(250)]
        public string direccion { get; set; } = string.Empty;

        [Required, MinLength(2), MaxLength(100)]
        public string ciudad { get; set; } = string.Empty;

        [Required, MinLength(2), MaxLength(100)]
        public string departamento { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? notas { get; set; }
    }
}
