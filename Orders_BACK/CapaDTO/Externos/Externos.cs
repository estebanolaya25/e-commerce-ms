using CapaDto.Respuestas;

namespace CapaDto.Externos
{
    // Respuesta de GET /api/carrito (microservicio Carrito).
    public class CarritoExternoDto
    {
        public List<ItemCarritoExternoDto> Items { get; set; } = new();
        public decimal Total { get; set; }
        public bool PuedeComprar { get; set; }
        public bool InformacionCompleta { get; set; }
        public List<string> Avisos { get; set; } = new();
    }

    public class ItemCarritoExternoDto
    {
        public string Sku { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public string? Nombre { get; set; }
        public Dictionary<string, string> Opciones { get; set; } = new();
        public string? ImagenUrl { get; set; }
        public decimal? PrecioUnitario { get; set; }
        public string Estado { get; set; } = string.Empty;
    }

    public enum EstadoLlamada
    {
        Exitosa,
        Conflicto,
        NoEncontrado,
        DatosInvalidos,
        NoDisponible
    }

    public class ResultadoReservaExterna
    {
        public EstadoLlamada Estado { get; set; }
        public string? EstadoReserva { get; set; }
        public List<FaltanteDto> Faltantes { get; set; } = new();
    }

    public class PagoExternoDto
    {
        public Guid PagoId { get; set; }
        public Guid PedidoId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string Marca { get; set; } = string.Empty;
        public string Ultimos4 { get; set; } = string.Empty;
    }

    public class ResultadoPagoExterno
    {
        public EstadoLlamada Estado { get; set; }
        public PagoExternoDto? Pago { get; set; }
        public string? Mensaje { get; set; }
    }
}
