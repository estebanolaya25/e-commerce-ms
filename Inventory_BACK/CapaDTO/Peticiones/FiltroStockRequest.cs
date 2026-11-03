using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class FiltroStockRequest
    {
        [Range(1, 100000)]
        public int pagina { get; set; } = 1;

        [Range(1, 100)]
        public int tamano { get; set; } = 20;

        [MaxLength(50)]
        public string? sku { get; set; }

        public bool soloBajoMinimo { get; set; }

        public bool soloAgotados { get; set; }
    }
}
