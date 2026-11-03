using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class FiltroPedidosRequest
    {
        [Range(1, 100000)]
        public int pagina { get; set; } = 1;

        [Range(1, 50)]
        public int tamano { get; set; } = 10;

        [MaxLength(20)]
        public string? estado { get; set; }

        [MaxLength(20)]
        public string? numero { get; set; }
    }

    public class CambiarEstadoPedidoRequest
    {
        [Required, MaxLength(20)]
        public string estado { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? motivo { get; set; }
    }
}
