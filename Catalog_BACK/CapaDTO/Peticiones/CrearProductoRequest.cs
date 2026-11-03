using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class CrearProductoRequest : ProductoRequest
    {
        [Required, MinLength(1, ErrorMessage = "El producto debe tener al menos una variante.")]
        public List<VarianteRequest> variantes { get; set; } = new();
    }
}
