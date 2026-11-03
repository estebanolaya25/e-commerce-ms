using CapaNegocios.Interfaz.pagos.interfaz;

namespace Payments_Api.tareas
{
    // Hace las veces del "banco": cada segundo resuelve los pagos cuya espera simulada ya terminó.
    public class ProcesadorPagosService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ProcesadorPagosService> _logger;

        public ProcesadorPagosService(IServiceScopeFactory scopeFactory, ILogger<ProcesadorPagosService> logger)
        {
            _scopeFactory = scopeFactory;
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
                    await scope.ServiceProvider.GetRequiredService<IPagosCapaNegocios>().ProcesarPendientes();
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error procesando pagos pendientes; se reintentará.");
                }
            }
        }
    }
}
