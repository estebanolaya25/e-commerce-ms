using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class VarianteRequest
    {
        [Required, MaxLength(50)]
        [RegularExpression("^[A-Za-z0-9_-]+$", ErrorMessage = "El SKU solo admite letras, números, guion y guion bajo.")]
        public string sku { get; set; } = string.Empty;

        public Dictionary<string, string> opciones { get; set; } = new();

        [Range(typeof(decimal), "0", "9999999999")]
        public decimal? precio { get; set; }

        [Range(typeof(decimal), "0", "9999999999")]
        public decimal? precioAntes { get; set; }
    }
}
