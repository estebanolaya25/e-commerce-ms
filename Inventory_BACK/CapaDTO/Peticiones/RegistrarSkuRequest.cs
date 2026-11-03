using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class RegistrarSkuRequest
    {
        [Required, MaxLength(50)]
        [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "El SKU solo admite letras, números, guion y guion bajo.")]
        public string sku { get; set; } = string.Empty;

        public Guid? productoId { get; set; }

        public Guid? varianteId { get; set; }
    }
}
