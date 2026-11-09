using System.Globalization;
using System.Text;
using Assistant_Api.Modelos;
using Microsoft.Extensions.Options;

namespace Assistant_Api.Servicios;

/// <summary>Producto encontrado para una pregunta, con la disponibilidad consultada en ese momento.</summary>
public record ProductoCandidato(
    string Slug,
    string Nombre,
    string? Marca,
    string Categoria,
    decimal Precio,
    decimal? PrecioAntes,
    string? ImagenUrl,
    string Colores,
    string Tallas,
    bool? Disponible,
    float Distancia)
{
    public ProductoSugerido ATarjeta() => new(Slug, Nombre, Marca, Precio, PrecioAntes, ImagenUrl, Disponible);
}

/// <summary>
/// Mantiene en ChromaDB un documento por producto activo (nombre, categoría, marca, colores, tallas y descripción) para
/// buscar por significado: "algo abrigado para la lluvia" encuentra "Chaqueta impermeable". El índice se llena con una
/// sincronización completa y se mantiene al día con los eventos de Catálogo; el precio se guarda como metadato y la
/// disponibilidad se consulta a Inventario en cada búsqueda porque cambia con cada compra.
/// </summary>
public class ProductosIndiceService(
    ChromaService chroma,
    EmbeddingService embeddings,
    CatalogoCliente catalogo,
    InventarioCliente inventario,
    IOptions<ProductosOptions> options,
    ILogger<ProductosIndiceService> logger)
{
    private const int TamanoPagina = 50;
    private const int LoteEmbeddings = 32;
    private readonly ProductosOptions _options = options.Value;

    // Una sola sincronización completa a la vez (arranque, evento de catálogo o el endpoint de administración).
    private static readonly SemaphoreSlim Sincronizando = new(1, 1);

    public async Task<int> CountAsync(CancellationToken ct) =>
        await chroma.CountAsync(await chroma.GetOrCreateCollectionAsync(_options.Coleccion, ct), ct);

    /// <summary>Lee todo el catálogo activo, actualiza el índice y borra lo que ya no está a la venta.</summary>
    public async Task<ResultadoSincronizacion> SincronizarTodoAsync(CancellationToken ct)
    {
        await Sincronizando.WaitAsync(ct);
        try
        {
            var collectionId = await chroma.GetOrCreateCollectionAsync(_options.Coleccion, ct);

            var slugs = new List<string>();
            for (var pagina = 1; ; pagina++)
            {
                var resultado = await catalogo.ListarAsync(pagina, TamanoPagina, ct);
                slugs.AddRange(resultado.Items.Select(p => p.Slug));
                if (pagina >= resultado.TotalPaginas || resultado.Items.Count == 0)
                {
                    break;
                }
            }

            var productos = new List<ProductoCatalogo>();
            await Parallel.ForEachAsync(slugs,
                new ParallelOptions { MaxDegreeOfParallelism = _options.ParalelismoSincronizacion, CancellationToken = ct },
                async (slug, token) =>
                {
                    var producto = await catalogo.ObtenerAsync(slug, token);
                    if (producto is not null)
                    {
                        lock (productos)
                        {
                            productos.Add(producto);
                        }
                    }
                });

            foreach (var lote in productos.Chunk(LoteEmbeddings))
            {
                await UpsertAsync(collectionId, lote, ct);
            }

            var vigentes = productos.Select(p => p.Id.ToString()).ToHashSet();
            var sobrantes = (await chroma.GetIdsAsync(collectionId, ct)).Where(id => !vigentes.Contains(id)).ToList();
            await chroma.DeleteAsync(collectionId, sobrantes, ct);

            logger.LogInformation("Productos sincronizados: {Indexados} indexados, {Eliminados} eliminados", productos.Count, sobrantes.Count);
            return new ResultadoSincronizacion(productos.Count, sobrantes.Count);
        }
        finally
        {
            Sincronizando.Release();
        }
    }

    /// <summary>Vuelve a leer un producto: si sigue a la venta se actualiza, si no se quita del índice.</summary>
    public async Task ActualizarProductoAsync(Guid productoId, string slug, CancellationToken ct)
    {
        var collectionId = await chroma.GetOrCreateCollectionAsync(_options.Coleccion, ct);
        var producto = await catalogo.ObtenerAsync(slug, ct);

        // El evento trae el slug vigente; si Catálogo devuelve otro producto con ese slug, se ignora.
        if (producto is null || producto.Id != productoId)
        {
            await chroma.DeleteAsync(collectionId, [productoId.ToString()], ct);
            logger.LogInformation("Producto {ProductoId} ({Slug}) quitado del índice: ya no está a la venta", productoId, slug);
            return;
        }

        await UpsertAsync(collectionId, [producto], ct);
        logger.LogInformation("Producto {Slug} actualizado en el índice", slug);
    }

    /// <summary>
    /// Productos relacionados con la pregunta. Con rango de precio se buscan más vecinos y se filtra; luego se consulta
    /// la disponibilidad real. Si Inventario no responde, los productos se ofrecen igual con disponibilidad desconocida.
    /// </summary>
    public async Task<List<ProductoCandidato>> BuscarAsync(string pregunta, FiltroPrecio filtro, CancellationToken ct)
    {
        var collectionId = await chroma.GetOrCreateCollectionAsync(_options.Coleccion, ct);
        var vector = await embeddings.EmbedQueryAsync(pregunta, ct);
        var vecinos = await chroma.QueryAsync(collectionId, vector,
            filtro.Vacio ? _options.Candidatos : _options.CandidatosConFiltroPrecio, ct);

        var candidatos = vecinos
            .Where(v => v.Distance <= _options.DistanciaMaxima)
            .Select(v => (Resultado: v, Precio: Decimal(v.Metadata, "precio")))
            .Where(v => v.Precio is not null && filtro.Cumple(v.Precio.Value))
            .Take(_options.Candidatos)
            .ToList();
        if (candidatos.Count == 0)
        {
            return [];
        }

        Dictionary<string, int>? disponibles = null;
        try
        {
            var skus = candidatos.SelectMany(c => Skus(c.Resultado.Metadata));
            disponibles = await inventario.DisponiblesAsync(skus, ct);
        }
        catch (Exception ex) when (Fallos.EsTransitorio(ex) && !ct.IsCancellationRequested)
        {
            logger.LogWarning("No se pudo consultar la disponibilidad en Inventario: {Mensaje}", ex.Message);
        }

        return candidatos.Select(c =>
        {
            var m = c.Resultado.Metadata;
            bool? disponible = disponibles is null
                ? null
                : Skus(m).Any(sku => disponibles.GetValueOrDefault(sku) > 0);
            return new ProductoCandidato(
                m.GetValueOrDefault("slug", ""),
                m.GetValueOrDefault("nombre", ""),
                Texto(m, "marca"),
                m.GetValueOrDefault("categoria", ""),
                c.Precio!.Value,
                Decimal(m, "precioAntes"),
                Texto(m, "imagenUrl"),
                m.GetValueOrDefault("colores", ""),
                m.GetValueOrDefault("tallas", ""),
                disponible,
                c.Resultado.Distance);
        }).ToList();
    }

    private async Task UpsertAsync(string collectionId, IReadOnlyCollection<ProductoCatalogo> productos, CancellationToken ct)
    {
        var documentos = productos.Select(ADocumento).Where(d => d is not null).Select(d => d!).ToList();
        if (documentos.Count == 0)
        {
            return;
        }
        var vectores = await embeddings.EmbedDocumentsAsync(documentos.Select(d => d.Text), ct);
        await chroma.UpsertAsync(collectionId, documentos, vectores, ct);
    }

    /// <summary>
    /// Texto que se convierte en embedding y metadatos para mostrar la tarjeta. El slug de la categoría ("camisas-hombre")
    /// se agrega en palabras porque es lo único que dice si la prenda es de hombre, mujer o niños.
    /// </summary>
    private static ChromaDocument? ADocumento(ProductoCatalogo p)
    {
        var variantes = p.Variantes.Where(v => v.Activa).ToList();
        if (variantes.Count == 0)
        {
            return null;
        }

        var masBarata = variantes.MinBy(v => v.Precio)!;
        var colores = Opciones(variantes, "color");
        var tallas = Opciones(variantes, "talla");
        var imagen = p.Imagenes.OrderByDescending(i => i.EsPrincipal).ThenBy(i => i.Orden).FirstOrDefault();

        var texto = new StringBuilder()
            .AppendLine($"Producto: {p.Nombre}")
            .AppendLine($"Categoría: {p.Categoria} ({p.CategoriaSlug.Replace('-', ' ')})");
        if (!string.IsNullOrWhiteSpace(p.Marca)) texto.AppendLine($"Marca: {p.Marca}");
        if (colores.Length > 0) texto.AppendLine($"Colores: {colores}");
        if (tallas.Length > 0) texto.AppendLine($"Tallas: {tallas}");
        foreach (var (clave, valor) in p.Atributos ?? [])
        {
            texto.AppendLine($"{clave}: {valor}");
        }
        if (!string.IsNullOrWhiteSpace(p.DescripcionCorta)) texto.AppendLine(p.DescripcionCorta);
        if (!string.IsNullOrWhiteSpace(p.Descripcion)) texto.AppendLine(p.Descripcion);

        var metadatos = new Dictionary<string, string>
        {
            ["slug"] = p.Slug,
            ["nombre"] = p.Nombre,
            ["marca"] = p.Marca ?? "",
            ["categoria"] = p.Categoria,
            ["precio"] = masBarata.Precio.ToString(CultureInfo.InvariantCulture),
            ["precioAntes"] = masBarata.PrecioAntes > masBarata.Precio
                ? masBarata.PrecioAntes.Value.ToString(CultureInfo.InvariantCulture)
                : "",
            ["imagenUrl"] = imagen?.Url ?? "",
            ["colores"] = colores,
            ["tallas"] = tallas,
            ["skus"] = string.Join(',', variantes.Select(v => v.Sku))
        };
        return new ChromaDocument(p.Id.ToString(), texto.ToString().Trim(), metadatos);
    }

    private static string Opciones(List<VarianteCatalogo> variantes, string clave) =>
        string.Join(", ", variantes
            .Select(v => v.Opciones?.FirstOrDefault(o => o.Key.Equals(clave, StringComparison.OrdinalIgnoreCase)).Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<string> Skus(Dictionary<string, string> metadatos) =>
        metadatos.GetValueOrDefault("skus", "").Split(',', StringSplitOptions.RemoveEmptyEntries);

    private static string? Texto(Dictionary<string, string> metadatos, string clave) =>
        metadatos.GetValueOrDefault(clave) is { Length: > 0 } valor ? valor : null;

    private static decimal? Decimal(Dictionary<string, string> metadatos, string clave) =>
        decimal.TryParse(metadatos.GetValueOrDefault(clave), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
}
