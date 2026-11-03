using CapaDatos.Interfaz.outbox.interfaz;
using CapaDto.Modelos;
using Ecommerce.Contratos.Pagos;
using MassTransit;
using System.Text.Json;

namespace Payments_Api.mensajeria
{
    // Publica en RabbitMQ los eventos guardados en pag.Outbox. Si RabbitMQ está caído, quedan pendientes y se reintentan.
    // El MessageId del mensaje es el OutboxId: si el mismo evento se publicara dos veces, el consumidor lo reconoce.
    public class PublicadorOutboxService : BackgroundService
    {
        private static readonly Dictionary<string, Type> TiposEvento = new()
        {
            [nameof(PagoAprobado)] = typeof(PagoAprobado),
            [nameof(PagoRechazado)] = typeof(PagoRechazado),
            [nameof(PagoReembolsado)] = typeof(PagoReembolsado)
        };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBus _bus;
        private readonly ILogger<PublicadorOutboxService> _logger;

        public PublicadorOutboxService(IServiceScopeFactory scopeFactory, IBus bus, ILogger<PublicadorOutboxService> logger)
        {
            _scopeFactory = scopeFactory;
            _bus = bus;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    var outbox = scope.ServiceProvider.GetRequiredService<IOutboxCapaDatos>();
                    int publicados = await outbox.ProcesarPendientes(50, mensaje => Publicar(mensaje, stoppingToken));
                    if (publicados > 0)
                        _logger.LogInformation("Outbox: {Publicados} eventos publicados en RabbitMQ.", publicados);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error procesando el Outbox; se reintentará.");
                }
            }
        }

        private async Task Publicar(MensajeOutbox mensaje, CancellationToken stoppingToken)
        {
            if (!TiposEvento.TryGetValue(mensaje.Tipo, out Type? tipo))
                throw new InvalidOperationException($"Tipo de evento desconocido en el Outbox: {mensaje.Tipo}");

            object evento = JsonSerializer.Deserialize(mensaje.Contenido, tipo)!;
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            limite.CancelAfter(TimeSpan.FromSeconds(5));
            await _bus.Publish(evento, tipo, Pipe.Execute<PublishContext>(contexto => contexto.MessageId = mensaje.OutboxId), limite.Token);
        }
    }
}
