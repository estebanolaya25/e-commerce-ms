using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class CategoriaRequest
    {
        [Required, MaxLength(100)]
        public string nombre { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? slug { get; set; }

        public Guid? categoriaPadreId { get; set; }

        [MaxLength(2000)]
        public string? descripcion { get; set; }

        [Range(0, 10000)]
        public int orden { get; set; }
    }
}
