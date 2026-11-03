using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class ProductoRequest
    {
        [Required]
        public Guid categoriaId { get; set; }

        public Guid? marcaId { get; set; }

        [Required, MaxLength(200)]
        public string nombre { get; set; } = string.Empty;

        [MaxLength(220)]
        public string? slug { get; set; }

        [MaxLength(500)]
        public string? descripcionCorta { get; set; }

        [MaxLength(10000)]
        public string? descripcion { get; set; }

        [Range(typeof(decimal), "0", "9999999999")]
        public decimal precioBase { get; set; }

        public Dictionary<string, string> atributos { get; set; } = new();

        public bool destacado { get; set; }
    }
}
