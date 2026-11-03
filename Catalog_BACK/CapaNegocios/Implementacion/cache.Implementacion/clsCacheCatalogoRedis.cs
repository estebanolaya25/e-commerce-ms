using CapaNegocios.Interfaz.cache.interfaz;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CapaNegocios.Implementacion.cache.Implementacion
{
    public class clsCacheCatalogoRedis : ICacheCatalogo
    {
        private const string ClaveVersion = "catalogo:version";

        private readonly IDistributedCache _cache;
        private readonly ILogger<clsCacheCatalogoRedis> _logger;

        public clsCacheCatalogoRedis(IDistributedCache cache, ILogger<clsCacheCatalogoRedis> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> ObtenerOCrear<T>(string clave, TimeSpan duracion, Func<Task<T?>> consultar) where T : class
        {
            string? claveCompleta = null;
            try
            {
                string version = await ObtenerVersion();
                claveCompleta = $"catalogo:{version}:{clave}";

                string? json = await _cache.GetStringAsync(claveCompleta);
                if (json != null)
                    return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer la caché para {Clave}. Se consulta la base de datos.", clave);
                claveCompleta = null;
            }

            T? valor = await consultar();

            if (valor != null && claveCompleta != null)
            {
                try
                {
                    await _cache.SetStringAsync(
                        claveCompleta,
                        JsonSerializer.Serialize(valor),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = duracion });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo guardar en caché la clave {Clave}.", clave);
                }
            }

            return valor;
        }

        public async Task Invalidar()
        {
            try
            {
                await _cache.SetStringAsync(ClaveVersion, Guid.NewGuid().ToString("N"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo invalidar la caché del catálogo. Los datos en caché expirarán por tiempo.");
            }
        }

        private async Task<string> ObtenerVersion()
        {
            string? version = await _cache.GetStringAsync(ClaveVersion);
            if (version != null)
                return version;

            version = Guid.NewGuid().ToString("N");
            await _cache.SetStringAsync(ClaveVersion, version);
            return version;
        }
    }
}
