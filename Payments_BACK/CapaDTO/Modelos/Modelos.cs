using System.Text.Json;

namespace CapaDto.Modelos
{
    public static class ModoSimulacion
    {
        public const string Aprobar = "Aprobar";
        public const string Rechazar = "Rechazar";
        public const string Aleatorio = "Aleatorio";
    }

    public class NuevoPago
    {
        public Guid PedidoId { get; set; }
        public decimal Monto { get; set; }
        public string Marca { get; set; } = string.Empty;
        public string Ultimos4 { get; set; } = string.Empty;
        public string Titular { get; set; } = string.Empty;
        public string Modo { get; set; } = ModoSimulacion.Aleatorio;
        public DateTime ProcesarEn { get; set; }
    }

    public class PagoPorProcesar
    {
        public Guid PagoId { get; set; }
        public Guid PedidoId { get; set; }
        public decimal Monto { get; set; }
        public string Modo { get; set; } = ModoSimulacion.Aleatorio;
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
