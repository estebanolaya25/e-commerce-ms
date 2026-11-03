using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class ActualizarCantidadRequest
    {
        [Range(1, 10)]
        public int cantidad { get; set; }
    }
}
