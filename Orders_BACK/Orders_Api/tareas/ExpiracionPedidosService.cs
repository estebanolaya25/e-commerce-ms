using CapaNegocios.Interfaz.pedidos.interfaz;

namespace Orders_Api.tareas
{
    public class ExpiracionPedidosService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExpiracionPedidosService> _logger;

        public ExpiracionPedidosService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<ExpiracionPedidosService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int segundos = int.TryParse(_configuration["Pedidos:SegundosRevisionExpiracion"], out int valor) ? valor : 60;
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(segundos));

            do
            {
                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<ISagaPedidosCapaNegocios>().ExpirarVencidos();
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error expirando pedidos vencidos; se reintentará en el próximo ciclo.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
