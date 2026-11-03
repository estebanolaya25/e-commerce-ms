using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class FiltroProductosRequest
    {
        [Range(1, 100000)]
        public int pagina { get; set; } = 1;

        [Range(1, 50)]
        public int tamano { get; set; } = 20;

        [MaxLength(120)]
        public string? categoria { get; set; }

        [MaxLength(120)]
        public string? marca { get; set; }

        [MaxLength(100)]
        public string? buscar { get; set; }

        [Range(typeof(decimal), "0", "9999999999")]
        public decimal? precioMin { get; set; }

        [Range(typeof(decimal), "0", "9999999999")]
        public decimal? precioMax { get; set; }

        public bool soloDestacados { get; set; }

        [RegularExpression("^(recientes|precio_asc|precio_desc|nombre|relevancia)$", ErrorMessage = "Orden no válido. Use recientes, precio_asc, precio_desc, nombre o relevancia.")]
        public string orden { get; set; } = "recientes";
    }
}
