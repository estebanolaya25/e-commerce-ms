using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class ReservarRequest
    {
        [Required]
        public Guid pedidoId { get; set; }

        [Required, MinLength(1), MaxLength(100)]
        public List<ItemReservaRequest> items { get; set; } = new();
    }

    public class ItemReservaRequest
    {
        [Required, MaxLength(50)]
        public string sku { get; set; } = string.Empty;

        [Range(1, 100)]
        public int cantidad { get; set; }
    }
}
