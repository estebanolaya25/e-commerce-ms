using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class CambiarEstadoRequest
    {
        [Required]
        public bool? activo { get; set; }
    }
}
