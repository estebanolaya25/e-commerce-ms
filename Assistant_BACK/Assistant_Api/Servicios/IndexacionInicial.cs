using Microsoft.Extensions.Options;

namespace Assistant_Api.Servicios;

/// <summary>
/// Al arrancar, llena lo que esté vacío: la base de conocimiento y el índice de productos. Así un entorno nuevo
/// (por ejemplo `docker compose up`) responde sin que alguien tenga que llamar a los endpoints de administración.
/// Reintenta unos minutos porque ChromaDB, Ollama o Catálogo pueden tardar más en estar listos que este servicio.
/// Después de esto, los productos se mantienen al día con los eventos de Catálogo.
/// </summary>
public class IndexacionInicial(IServiceScopeFactory scopes, IOptions<RagOptions> options, ILogger<IndexacionInicial> logger)
    : BackgroundService
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);
    private const int Intentos = 18;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IndexarAlIniciar)
        {
            return;
        }

        await ConReintentosAsync("la base de conocimiento", async (sp, ct) =>
        {
            var knowledgeBase = sp.GetRequiredService<KnowledgeBaseService>();
            if (await knowledgeBase.CountAsync(ct) > 0)
            {
                return;
            }
            var resultado = await knowledgeBase.ReindexAsync(ct);
            logger.LogInformation("Base de conocimiento indexada: {Documentos} documentos, {Fragmentos} fragmentos",
                resultado.Documentos, resultado.Fragmentos);
        }, stoppingToken);

        await ConReintentosAsync("los productos", async (sp, ct) =>
        {
            var productos = sp.GetRequiredService<ProductosIndiceService>();
            if (await productos.CountAsync(ct) > 0)
            {
                return;
            }
            await productos.SincronizarTodoAsync(ct);
        }, stoppingToken);
    }

    private async Task ConReintentosAsync(string que, Func<IServiceProvider, CancellationToken, Task> tarea, CancellationToken stoppingToken)
    {
        for (var intento = 1; intento <= Intentos && !stoppingToken.IsCancellationRequested; intento++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await tarea(scope.ServiceProvider, stoppingToken);
                return;
            }
            catch (Exception ex) when (Fallos.EsTransitorio(ex) && !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("No se pudo indexar {Que} al iniciar (intento {Intento}/{Total}): {Mensaje}", que, intento, Intentos, ex.Message);
                await Task.Delay(Espera, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Un error no transitorio (p. ej. falta la carpeta de documentos) no debe tumbar el servicio:
                // en .NET una excepción sin capturar en un BackgroundService detiene toda la aplicación.
                logger.LogError(ex, "No se pudo indexar {Que} al iniciar", que);
                return;
            }
        }
    }
}
