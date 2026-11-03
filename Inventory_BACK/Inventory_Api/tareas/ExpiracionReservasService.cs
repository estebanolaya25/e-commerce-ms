using CapaNegocios.Interfaz.reservas.interfaz;

namespace Inventory_Api.tareas
{
    public class ExpiracionReservasService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExpiracionReservasService> _logger;

        public ExpiracionReservasService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ExpiracionReservasService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int segundos = int.TryParse(_configuration["Inventario:SegundosRevisionExpiracion"], out int valor) ? valor : 60;
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(segundos));

            do
            {
                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IReservasCapaNegocios>().ExpirarVencidas();
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error liberando reservas vencidas; se reintentará en el próximo ciclo.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
