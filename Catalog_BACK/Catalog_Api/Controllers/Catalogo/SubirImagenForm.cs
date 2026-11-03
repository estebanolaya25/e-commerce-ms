using System.ComponentModel.DataAnnotations;

namespace Catalog_Api.Controllers.Catalogo
{
    public class SubirImagenForm
    {
        [Required]
        public IFormFile archivo { get; set; } = null!;

        public Guid? varianteId { get; set; }

        [MaxLength(200)]
        public string? textoAlternativo { get; set; }

        public bool esPrincipal { get; set; }
    }
}
