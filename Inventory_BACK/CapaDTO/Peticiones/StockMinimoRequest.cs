using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class StockMinimoRequest
    {
        [Range(0, 100000)]
        public int stockMinimo { get; set; }
    }
}
