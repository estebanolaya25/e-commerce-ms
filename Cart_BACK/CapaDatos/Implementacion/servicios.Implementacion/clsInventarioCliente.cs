using CapaDatos.Interfaz.servicios.interfaz;
using CapaDto.Externos;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace CapaDatos.Implementacion.servicios.Implementacion
{
    public class clsInventarioCliente : IInventarioCliente
    {
        private readonly HttpClient _http;
        private readonly ILogger<clsInventarioCliente> _logger;

        public clsInventarioCliente(HttpClient http, ILogger<clsInventarioCliente> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<List<DisponibilidadInventarioDto>?> ObtenerDisponibilidad(IReadOnlyCollection<string> skus)
        {
            try
            {
                string ruta = "api/inventario/disponibilidad?skus=" + Uri.EscapeDataString(string.Join(',', skus));
                return await _http.GetFromJsonAsync<List<DisponibilidadInventarioDto>>(ruta) ?? new List<DisponibilidadInventarioDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inventario no respondió al consultar {Cantidad} SKU.", skus.Count);
                return null;
            }
        }
    }
}
