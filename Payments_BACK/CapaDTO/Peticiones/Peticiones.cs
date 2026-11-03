using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class IniciarPagoRequest
    {
        [Required]
        public Guid pedidoId { get; set; }

        [Range(typeof(decimal), "1", "9999999999")]
        public decimal monto { get; set; }

        [Required, RegularExpression(@"^[0-9 ]{13,23}$", ErrorMessage = "Número de tarjeta inválido.")]
        public string numeroTarjeta { get; set; } = string.Empty;

        [Required, MinLength(3), MaxLength(100)]
        public string titular { get; set; } = string.Empty;

        [Range(1, 12)]
        public int mesVencimiento { get; set; }

        [Range(2000, 2100)]
        public int anioVencimiento { get; set; }

        [Required, RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVV inválido.")]
        public string cvv { get; set; } = string.Empty;
    }

    public class ReembolsarRequest
    {
        [Required, MinLength(3), MaxLength(200)]
        public string motivo { get; set; } = string.Empty;
    }

    public class FiltroPagosRequest
    {
        [Range(1, 100000)]
        public int pagina { get; set; } = 1;

        [Range(1, 100)]
        public int tamano { get; set; } = 20;

        public Guid? pedidoId { get; set; }

        [MaxLength(20)]
        public string? estado { get; set; }
    }
}
