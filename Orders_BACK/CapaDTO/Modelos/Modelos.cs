using CapaDto.Respuestas;
using System.Text.Json;

namespace CapaDto.Modelos
{
    public class NuevoPedido
    {
        public Guid PedidoId { get; set; }
        public string Numero { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public string? UsuarioEmail { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal CostoEnvio { get; set; }
        public decimal Total { get; set; }
        public DireccionDto Direccion { get; set; } = new();
        public DateTime ExpiraEn { get; set; }
        public List<ItemPedidoDto> Items { get; set; } = new();
    }

    // Cambio de estado atómico: solo se aplica si el pedido está en uno de los estados permitidos.
    // NuevoEstado null deja el estado igual y solo registra una nota en el historial.
    public class TransicionPedido
    {
        public Guid PedidoId { get; set; }
        public string[] EstadosPermitidos { get; set; } = Array.Empty<string>();
        public string? NuevoEstado { get; set; }
        public string? Motivo { get; set; }
        public Guid? UsuarioId { get; set; }
        public Guid? PagoId { get; set; }
        public bool ActualizarErrorPago { get; set; }
        public string? ErrorPago { get; set; }
        public EventoOutbox? Evento { get; set; }
        public Guid? MensajeId { get; set; }
        public string? TipoMensaje { get; set; }
    }

    public enum ResultadoTransicion
    {
        Aplicada,
        EstadoNoPermitido,
        MensajeDuplicado
    }

    public class EventoOutbox
    {
        public string Tipo { get; init; } = string.Empty;
        public string Contenido { get; init; } = string.Empty;

        public static EventoOutbox Crear<T>(T evento) where T : class => new()
        {
            Tipo = typeof(T).Name,
            Contenido = JsonSerializer.Serialize(evento)
        };
    }

    public class MensajeOutbox
    {
        public Guid OutboxId { get; init; }
        public string Tipo { get; init; } = string.Empty;
        public string Contenido { get; init; } = string.Empty;
    }
}
