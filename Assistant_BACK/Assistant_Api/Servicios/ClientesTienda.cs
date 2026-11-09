using System.Net;

namespace Assistant_Api.Servicios;

// Solo los campos que el asistente usa de las respuestas de Catálogo e Inventario.
public record ProductoResumenCatalogo(Guid Id, string Slug);

public record PaginaCatalogo(List<ProductoResumenCatalogo> Items, int Pagina, int TotalPaginas);

public record VarianteCatalogo(string Sku, Dictionary<string, string>? Opciones, decimal Precio, decimal? PrecioAntes, bool Activa);

public record ImagenCatalogo(string Url, string? TextoAlternativo, int Orden, bool EsPrincipal);

public record ProductoCatalogo(
    Guid Id,
    string Nombre,
    string Slug,
    string? DescripcionCorta,
    string? Descripcion,
    decimal PrecioBase,
    Dictionary<string, string>? Atributos,
    string Categoria,
    string CategoriaSlug,
    string? Marca,
    List<VarianteCatalogo> Variantes,
    List<ImagenCatalogo> Imagenes);

public record DisponibilidadInventario(string Sku, bool Existe, int Disponible);

/// <summary>Lee el catálogo público (lo mismo que ve un cliente: solo productos activos).</summary>
public class CatalogoCliente(HttpClient http)
{
    public async Task<PaginaCatalogo> ListarAsync(int pagina, int tamano, CancellationToken ct) =>
        await http.GetFromJsonAsync<PaginaCatalogo>($"api/catalogo/productos?pagina={pagina}&tamano={tamano}&orden=nombre", ct)
            ?? new PaginaCatalogo([], pagina, 0);

    /// <summary>Nombre y opciones de varias variantes por SKU (endpoint público, máximo 50 por consulta).</summary>
    public async Task<Dictionary<string, VarianteResumenCatalogo>> VariantesAsync(IEnumerable<string> skus, CancellationToken ct)
    {
        var resultado = new Dictionary<string, VarianteResumenCatalogo>(StringComparer.OrdinalIgnoreCase);
        foreach (var lote in skus.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(50))
        {
            var url = $"api/catalogo/variantes?skus={Uri.EscapeDataString(string.Join(',', lote))}";
            foreach (var v in await http.GetFromJsonAsync<List<VarianteResumenCatalogo>>(url, ct) ?? [])
            {
                resultado[v.Sku] = v;
            }
        }
        return resultado;
    }

    /// <summary>null si el producto no existe o ya no está a la venta (inactivo, sin variantes activas…).</summary>
    public async Task<ProductoCatalogo?> ObtenerAsync(string slug, CancellationToken ct)
    {
        using var response = await http.GetAsync($"api/catalogo/productos/{Uri.EscapeDataString(slug)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductoCatalogo>(ct);
    }
}

/// <summary>Consulta las unidades disponibles por SKU (endpoint público de Inventario).</summary>
public class InventarioCliente(HttpClient http)
{
    private const int MaxSkusPorConsulta = 100;

    public async Task<Dictionary<string, int>> DisponiblesAsync(IEnumerable<string> skus, CancellationToken ct)
    {
        var resultado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var lote in skus.Distinct(StringComparer.OrdinalIgnoreCase).Chunk(MaxSkusPorConsulta))
        {
            var url = $"api/inventario/disponibilidad?skus={Uri.EscapeDataString(string.Join(',', lote))}";
            foreach (var item in await http.GetFromJsonAsync<List<DisponibilidadInventario>>(url, ct) ?? [])
            {
                resultado[item.Sku] = item.Existe ? item.Disponible : 0;
            }
        }
        return resultado;
    }
}
