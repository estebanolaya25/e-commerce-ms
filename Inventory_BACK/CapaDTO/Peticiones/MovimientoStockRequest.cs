using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class MovimientoStockRequest
    {
        [Range(-100000, 100000)]
        public int cantidad { get; set; }

        [Required, MinLength(3), MaxLength(250)]
        public string motivo { get; set; } = string.Empty;
    }
}
