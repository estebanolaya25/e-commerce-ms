using Microsoft.Extensions.Options;

namespace Assistant_Api.Servicios;

/// <summary>
/// Al arrancar, si la colección de ChromaDB está vacía, indexa la base de conocimiento. Así un entorno nuevo
/// (por ejemplo `docker compose up`) responde sin que alguien tenga que llamar a /reindexar. Reintenta unos minutos
/// porque ChromaDB u Ollama pueden tardar más en estar listos que este servicio.
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

        for (var intento = 1; intento <= Intentos && !stoppingToken.IsCancellationRequested; intento++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var knowledgeBase = scope.ServiceProvider.GetRequiredService<KnowledgeBaseService>();
                if (await knowledgeBase.CountAsync(stoppingToken) > 0)
                {
                    return;
                }

                var resultado = await knowledgeBase.ReindexAsync(stoppingToken);
                logger.LogInformation("Base de conocimiento indexada: {Documentos} documentos, {Fragmentos} fragmentos",
                    resultado.Documentos, resultado.Fragmentos);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("No se pudo indexar al iniciar (intento {Intento}/{Total}): {Mensaje}", intento, Intentos, ex.Message);
                await Task.Delay(Espera, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Un error no transitorio (p. ej. falta la carpeta de documentos) no debe tumbar el servicio:
                // en .NET una excepción sin capturar en un BackgroundService detiene toda la aplicación.
                logger.LogError(ex, "No se pudo indexar la base de conocimiento al iniciar");
                return;
            }
        }
    }
}
