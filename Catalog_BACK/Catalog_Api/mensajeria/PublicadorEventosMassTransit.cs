using CapaNegocios.Interfaz.eventos.interfaz;
using MassTransit;

namespace Catalog_Api.mensajeria
{
    public class PublicadorEventosMassTransit : IPublicadorEventos
    {
        private static readonly TimeSpan TiempoMaximo = TimeSpan.FromSeconds(5);

        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<PublicadorEventosMassTransit> _logger;

        public PublicadorEventosMassTransit(IPublishEndpoint publishEndpoint, ILogger<PublicadorEventosMassTransit> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Publicar<T>(T evento) where T : class
        {
            using var cancelacion = new CancellationTokenSource(TiempoMaximo);
            try
            {
                await _publishEndpoint.Publish(evento, cancelacion.Token);
            }
            catch (Exception ex)
            {
                // El cambio ya quedó guardado en la base; no se revierte por un fallo de mensajería.
                // Para no perder eventos se usaría el patrón Outbox (pendiente).
                _logger.LogError(ex, "No se pudo publicar el evento {Tipo}: {@Evento}", typeof(T).Name, evento);
            }
        }
    }
}
