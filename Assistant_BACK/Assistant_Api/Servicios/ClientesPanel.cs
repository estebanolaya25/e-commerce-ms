namespace Assistant_Api.Servicios;

// Respuestas de los endpoints internos (panel /admin). Igual que con los clientes, se omiten teléfono y dirección:
// el modelo no los necesita para responder al personal.
public record PedidoResumenPanel(Guid PedidoId, string Numero, string Estado, decimal Total, int Unidades, DateTime ExpiraEn, DateTime CreatedAt);

public record PaginaPedidosPanel(List<PedidoResumenPanel> Items, int Total);

public record PedidoPanel(
    Guid PedidoId,
    string Numero,
    string? UsuarioEmail,
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

public record PagoPanel(Guid PagoId, decimal Monto, string Estado, string Marca, string Ultimos4, string? Referencia,
    string? MotivoRechazo, DateTime CreatedAt, DateTime? ProcesadoEn, DateTime? ReembolsadoEn);

public record PaginaPagos(List<PagoPanel> Items, int Total);

public record StockPanel(string Sku, int CantidadFisica, int CantidadReservada, int CantidadDisponible, int StockMinimo, DateTime UpdatedAt);

public record PaginaStock(List<StockPanel> Items, int Total);

public record MovimientoStock(string Tipo, int Cantidad, int FisicaResultante, string? Motivo, DateTime CreatedAt);

public record VarianteResumenCatalogo(string Sku, string Nombre, string Slug, Dictionary<string, string>? Opciones);

/// <summary>Pedidos para el personal: Pedidos decide qué ve cada rol (Bodega solo ve pedidos pagados y sin correo).</summary>
public class PanelPedidosCliente(HttpClient http) : ClienteConToken(http)
{
    public async Task<PaginaPedidosPanel> BuscarAsync(string token, string? estado, string? numero, int tamano, CancellationToken ct)
    {
        var ruta = $"api/pedidos/admin?pagina=1&tamano={tamano}";
        if (!string.IsNullOrWhiteSpace(estado)) ruta += $"&estado={Uri.EscapeDataString(estado)}";
        if (!string.IsNullOrWhiteSpace(numero)) ruta += $"&numero={Uri.EscapeDataString(numero.Trim().ToUpperInvariant())}";
        return await GetAsync<PaginaPedidosPanel>(ruta, token, ct) ?? new PaginaPedidosPanel([], 0);
    }

    public async Task<PedidoPanel?> PorNumeroAsync(string token, string numero, CancellationToken ct)
    {
        var pagina = await BuscarAsync(token, null, numero, 1, ct);
        var resumen = pagina.Items.FirstOrDefault(p => p.Numero.Equals(numero.Trim(), StringComparison.OrdinalIgnoreCase));
        return resumen is null ? null : await GetAsync<PedidoPanel>($"api/pedidos/admin/{resumen.PedidoId}", token, ct);
    }
}

public class PagosCliente(HttpClient http) : ClienteConToken(http)
{
    public async Task<List<PagoPanel>> DelPedidoAsync(string token, Guid pedidoId, CancellationToken ct) =>
        (await GetAsync<PaginaPagos>($"api/pagos?pagina=1&tamano=20&pedidoId={pedidoId}", token, ct))?.Items ?? [];
}

public class StockCliente(HttpClient http) : ClienteConToken(http)
{
    public async Task<PaginaStock> BajoMinimoAsync(string token, bool soloAgotados, int tamano, CancellationToken ct) =>
        await GetAsync<PaginaStock>(
            $"api/inventario/stock?pagina=1&tamano={tamano}&{(soloAgotados ? "soloAgotados" : "soloBajoMinimo")}=true", token, ct)
        ?? new PaginaStock([], 0);

    public Task<StockPanel?> PorSkuAsync(string token, string sku, CancellationToken ct) =>
        GetAsync<StockPanel>($"api/inventario/stock/{Uri.EscapeDataString(sku)}", token, ct);

    public async Task<List<MovimientoStock>> MovimientosAsync(string token, string sku, int limite, CancellationToken ct) =>
        await GetAsync<List<MovimientoStock>>($"api/inventario/stock/{Uri.EscapeDataString(sku)}/movimientos?limite={limite}", token, ct) ?? [];
}
