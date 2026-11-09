using System.Net;
using System.Net.Http.Headers;

namespace Assistant_Api.Servicios;

// Solo los campos que el asistente usa de Pedidos y Carrito. La dirección de envío se omite a propósito, salvo la
// ciudad: el modelo no necesita teléfono ni dirección para responder y así esos datos no salen hacia Ollama.
public record PedidoResumenUsuario(Guid PedidoId, string Numero, string Estado, decimal Total, int Unidades, DateTime ExpiraEn, DateTime CreatedAt);

public record PaginaPedidos(List<PedidoResumenUsuario> Items, int Total);

public record DireccionPedido(string Ciudad);

public record ItemPedido(string Nombre, string? Opciones, decimal PrecioUnitario, int Cantidad, decimal Subtotal);

public record HistorialPedido(string? EstadoAnterior, string EstadoNuevo, string? Motivo, DateTime CreatedAt);

public record PedidoUsuario(
    Guid PedidoId,
    string Numero,
    string Estado,
    decimal Subtotal,
    decimal CostoEnvio,
    decimal Total,
    DireccionPedido? Direccion,
    string? UltimoErrorPago,
    DateTime ExpiraEn,
    DateTime CreatedAt,
    List<ItemPedido> Items,
    List<HistorialPedido> Historial);

public record ItemCarrito(string Sku, int Cantidad, string? Nombre, string? Slug, Dictionary<string, string>? Opciones,
    decimal? PrecioUnitario, decimal? Subtotal, int? StockDisponible, string Estado);

public record CarritoUsuario(List<ItemCarrito> Items, int Unidades, decimal Total, bool PuedeComprar, List<string>? Avisos);

/// <summary>
/// Llamadas a Pedidos y Carrito en nombre del cliente: se reenvía SU access token, nunca una credencial del asistente.
/// Así cada servicio aplica sus propias reglas (un cliente solo ve sus pedidos) aunque el modelo pidiera otra cosa.
/// </summary>
public abstract class ClienteConToken(HttpClient http)
{
    protected async Task<T?> GetAsync<T>(string ruta, string token, CancellationToken ct) where T : class
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuesta = await http.SendAsync(peticion, ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<T>(ct);
    }
}

public class PedidosCliente(HttpClient http) : ClienteConToken(http)
{
    public async Task<PaginaPedidos> RecientesAsync(string token, int cantidad, CancellationToken ct) =>
        await GetAsync<PaginaPedidos>($"api/pedidos?pagina=1&tamano={cantidad}", token, ct) ?? new PaginaPedidos([], 0);

    /// <summary>Busca por número entre los pedidos del cliente (Pedidos filtra por el usuario del token).</summary>
    public async Task<PedidoUsuario?> PorNumeroAsync(string token, string numero, CancellationToken ct)
    {
        var pagina = await GetAsync<PaginaPedidos>($"api/pedidos?pagina=1&tamano=1&numero={Uri.EscapeDataString(numero)}", token, ct);
        var resumen = pagina?.Items.FirstOrDefault(p => p.Numero.Equals(numero, StringComparison.OrdinalIgnoreCase));
        return resumen is null ? null : await GetAsync<PedidoUsuario>($"api/pedidos/{resumen.PedidoId}", token, ct);
    }
}

public class CarritoCliente(HttpClient http) : ClienteConToken(http)
{
    public async Task<CarritoUsuario> ObtenerAsync(string token, CancellationToken ct) =>
        await GetAsync<CarritoUsuario>("api/carrito", token, ct) ?? new CarritoUsuario([], 0, 0, false, []);
}
