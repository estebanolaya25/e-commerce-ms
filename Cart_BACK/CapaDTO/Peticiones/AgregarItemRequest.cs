using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class AgregarItemRequest
    {
        [Required, MaxLength(50)]
        public string sku { get; set; } = string.Empty;

        [Range(1, 10)]
        public int cantidad { get; set; } = 1;
    }
}
