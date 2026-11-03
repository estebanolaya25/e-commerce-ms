using CapaDatos.Interfaz.servicios.interfaz;
using CapaDto.Externos;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace CapaDatos.Implementacion.servicios.Implementacion
{
    // HttpClient tipado: la URL base, los timeouts, reintentos y circuit breaker se configuran en Program.cs.
    public class clsCatalogoCliente : ICatalogoCliente
    {
        private readonly HttpClient _http;
        private readonly ILogger<clsCatalogoCliente> _logger;

        public clsCatalogoCliente(HttpClient http, ILogger<clsCatalogoCliente> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<List<VarianteCatalogoDto>?> ObtenerVariantes(IReadOnlyCollection<string> skus)
        {
            try
            {
                string ruta = "api/catalogo/variantes?skus=" + Uri.EscapeDataString(string.Join(',', skus));
                return await _http.GetFromJsonAsync<List<VarianteCatalogoDto>>(ruta) ?? new List<VarianteCatalogoDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catálogo no respondió al consultar {Cantidad} SKU.", skus.Count);
                return null;
            }
        }
    }
}
