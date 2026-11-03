using CapaDatos.Interfaz.servicios.interfaz;
using CapaDto.Externos;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CapaDatos.Implementacion.servicios.Implementacion
{
    // HttpClients tipados: URL base, clave interna, timeouts, reintentos y circuit breaker se configuran en Program.cs.
    public class clsCarritoCliente : ICarritoCliente
    {
        private readonly HttpClient _http;
        private readonly ILogger<clsCarritoCliente> _logger;

        public clsCarritoCliente(HttpClient http, ILogger<clsCarritoCliente> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<CarritoExternoDto?> ObtenerCarrito(string autorizacionUsuario)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/carrito");
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(autorizacionUsuario);
                using HttpResponseMessage response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<CarritoExternoDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Carrito no respondió.");
                return null;
            }
        }
    }

    public class clsInventarioCliente : IInventarioCliente
    {
        private readonly HttpClient _http;
        private readonly ILogger<clsInventarioCliente> _logger;

        public clsInventarioCliente(HttpClient http, ILogger<clsInventarioCliente> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<ResultadoReservaExterna> Reservar(Guid pedidoId, IEnumerable<(string Sku, int Cantidad)> items)
        {
            try
            {
                var cuerpo = new { pedidoId, items = items.Select(i => new { sku = i.Sku, cantidad = i.Cantidad }) };
                using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/inventario/reservas", cuerpo);

                if (response.IsSuccessStatusCode)
                {
                    var reserva = await response.Content.ReadFromJsonAsync<ReservaRespuesta>();
                    return new ResultadoReservaExterna { Estado = EstadoLlamada.Exitosa, EstadoReserva = reserva?.Estado };
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    var conflicto = await response.Content.ReadFromJsonAsync<ConflictoRespuesta>();
                    return new ResultadoReservaExterna { Estado = EstadoLlamada.Conflicto, Faltantes = conflicto?.Faltantes ?? new() };
                }

                _logger.LogWarning("Inventario respondió {Codigo} al reservar el pedido {PedidoId}.", (int)response.StatusCode, pedidoId);
                return new ResultadoReservaExterna { Estado = Clasificar(response.StatusCode) };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inventario no respondió al reservar el pedido {PedidoId}.", pedidoId);
                return new ResultadoReservaExterna { Estado = EstadoLlamada.NoDisponible };
            }
        }

        public Task<EstadoLlamada> Confirmar(Guid pedidoId) => Ejecutar($"internal/inventario/reservas/{pedidoId}/confirmar", pedidoId);

        public Task<EstadoLlamada> Liberar(Guid pedidoId) => Ejecutar($"internal/inventario/reservas/{pedidoId}/liberar", pedidoId);

        private async Task<EstadoLlamada> Ejecutar(string ruta, Guid pedidoId)
        {
            try
            {
                using HttpResponseMessage response = await _http.PostAsync(ruta, null);
                return response.IsSuccessStatusCode ? EstadoLlamada.Exitosa : Clasificar(response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inventario no respondió en {Ruta} para el pedido {PedidoId}.", ruta, pedidoId);
                return EstadoLlamada.NoDisponible;
            }
        }

        private static EstadoLlamada Clasificar(HttpStatusCode codigo) => codigo switch
        {
            HttpStatusCode.Conflict => EstadoLlamada.Conflicto,
            HttpStatusCode.NotFound => EstadoLlamada.NoEncontrado,
            HttpStatusCode.BadRequest => EstadoLlamada.DatosInvalidos,
            _ => EstadoLlamada.NoDisponible
        };

        private sealed class ReservaRespuesta
        {
            public string Estado { get; set; } = string.Empty;
        }

        private sealed class ConflictoRespuesta
        {
            public List<FaltanteDto>? Faltantes { get; set; }
        }
    }

    public class clsPagosCliente : IPagosCliente
    {
        private readonly HttpClient _http;
        private readonly ILogger<clsPagosCliente> _logger;

        public clsPagosCliente(HttpClient http, ILogger<clsPagosCliente> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<ResultadoPagoExterno> IniciarPago(Guid pedidoId, decimal monto, TarjetaRequest tarjeta)
        {
            try
            {
                var cuerpo = new
                {
                    pedidoId,
                    monto,
                    numeroTarjeta = tarjeta.numero,
                    titular = tarjeta.titular,
                    mesVencimiento = tarjeta.mesVencimiento,
                    anioVencimiento = tarjeta.anioVencimiento,
                    cvv = tarjeta.cvv
                };
                using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/pagos", cuerpo);

                if (response.IsSuccessStatusCode)
                    return new ResultadoPagoExterno { Estado = EstadoLlamada.Exitosa, Pago = await response.Content.ReadFromJsonAsync<PagoExternoDto>() };

                var error = await LeerMensaje(response);
                return new ResultadoPagoExterno
                {
                    Estado = response.StatusCode switch
                    {
                        HttpStatusCode.BadRequest => EstadoLlamada.DatosInvalidos,
                        HttpStatusCode.Conflict => EstadoLlamada.Conflicto,
                        _ => EstadoLlamada.NoDisponible
                    },
                    Mensaje = error
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pagos no respondió al iniciar el pago del pedido {PedidoId}.", pedidoId);
                return new ResultadoPagoExterno { Estado = EstadoLlamada.NoDisponible };
            }
        }

        public async Task<EstadoLlamada> Reembolsar(Guid pagoId, string motivo)
        {
            try
            {
                using HttpResponseMessage response = await _http.PostAsJsonAsync($"internal/pagos/{pagoId}/reembolsar", new { motivo });
                return response.IsSuccessStatusCode ? EstadoLlamada.Exitosa
                    : response.StatusCode == HttpStatusCode.Conflict ? EstadoLlamada.Conflicto
                    : response.StatusCode == HttpStatusCode.NotFound ? EstadoLlamada.NoEncontrado
                    : EstadoLlamada.NoDisponible;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pagos no respondió al reembolsar el pago {PagoId}.", pagoId);
                return EstadoLlamada.NoDisponible;
            }
        }

        private static async Task<string?> LeerMensaje(HttpResponseMessage response)
        {
            try
            {
                var cuerpo = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                return cuerpo != null && cuerpo.TryGetValue("mensaje", out object? mensaje) ? mensaje?.ToString() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
