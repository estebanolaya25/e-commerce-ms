using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    // Pedidos no guarda los datos de la tarjeta: solo los reenvía a Pagos.
    public class PagarRequest
    {
        [Required]
        public TarjetaRequest tarjeta { get; set; } = new();
    }

    public class TarjetaRequest
    {
        [Required, RegularExpression(@"^[0-9 ]{13,23}$", ErrorMessage = "Número de tarjeta inválido.")]
        public string numero { get; set; } = string.Empty;

        [Required, MinLength(3), MaxLength(100)]
        public string titular { get; set; } = string.Empty;

        [Range(1, 12)]
        public int mesVencimiento { get; set; }

        [Range(2000, 2100)]
        public int anioVencimiento { get; set; }

        [Required, RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVV inválido.")]
        public string cvv { get; set; } = string.Empty;
    }
}
