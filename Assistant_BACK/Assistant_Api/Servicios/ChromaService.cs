using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Assistant_Api.Servicios;

public record ChromaDocument(string Id, string Text, Dictionary<string, string> Metadata);

public record ChromaResult(string Text, Dictionary<string, string> Metadata, float Distance);

/// <summary>Cliente mínimo de la API HTTP v2 de ChromaDB.</summary>
public class ChromaService(HttpClient http, IOptions<RagOptions> options)
{
    private const string CollectionsPath = "/api/v2/tenants/default_tenant/databases/default_database/collections";
    private readonly RagOptions _options = options.Value;

    public async Task<string> GetOrCreateCollectionAsync(CancellationToken ct)
    {
        var request = new CreateCollectionRequest(
            _options.Collection,
            GetOrCreate: true,
            new CollectionConfiguration(new HnswConfiguration("cosine")));

        using var response = await http.PostAsJsonAsync(CollectionsPath, request, ct);
        await EnsureSuccessAsync(response, ct);
        var collection = await response.Content.ReadFromJsonAsync<CollectionResponse>(ct);
        return collection!.Id;
    }

    public async Task DeleteCollectionAsync(CancellationToken ct)
    {
        using var response = await http.DeleteAsync($"{CollectionsPath}/{_options.Collection}", ct);
        // 404 = la colección aún no existe; no es un error.
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(response, ct);
        }
    }

    public async Task AddAsync(string collectionId, List<ChromaDocument> documents, List<float[]> embeddings, CancellationToken ct)
    {
        var request = new AddRequest(
            documents.Select(d => d.Id).ToList(),
            embeddings,
            documents.Select(d => d.Text).ToList(),
            documents.Select(d => d.Metadata).ToList());

        using var response = await http.PostAsJsonAsync($"{CollectionsPath}/{collectionId}/add", request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<int> CountAsync(string collectionId, CancellationToken ct) =>
        await http.GetFromJsonAsync<int>($"{CollectionsPath}/{collectionId}/count", ct);

    public async Task<List<ChromaResult>> QueryAsync(string collectionId, float[] embedding, int topK, CancellationToken ct)
    {
        var request = new QueryRequest([embedding], topK, ["documents", "metadatas", "distances"]);

        using var response = await http.PostAsJsonAsync($"{CollectionsPath}/{collectionId}/query", request, ct);
        await EnsureSuccessAsync(response, ct);
        var result = await response.Content.ReadFromJsonAsync<QueryResponse>(ct);
        if (result is null || result.Documents.Count == 0)
        {
            return [];
        }

        return result.Documents[0]
            .Select((text, i) => new ChromaResult(
                text ?? string.Empty,
                result.Metadatas[0][i] ?? [],
                result.Distances[0][i]))
            .ToList();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"ChromaDB respondió {(int)response.StatusCode}: {error}");
        }
    }

    private record HnswConfiguration([property: JsonPropertyName("space")] string Space);

    private record CollectionConfiguration([property: JsonPropertyName("hnsw")] HnswConfiguration Hnsw);

    private record CreateCollectionRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("get_or_create")] bool GetOrCreate,
        [property: JsonPropertyName("configuration")] CollectionConfiguration Configuration);

    private record CollectionResponse([property: JsonPropertyName("id")] string Id);

    private record AddRequest(
        [property: JsonPropertyName("ids")] List<string> Ids,
        [property: JsonPropertyName("embeddings")] List<float[]> Embeddings,
        [property: JsonPropertyName("documents")] List<string> Documents,
        [property: JsonPropertyName("metadatas")] List<Dictionary<string, string>> Metadatas);

    private record QueryRequest(
        [property: JsonPropertyName("query_embeddings")] List<float[]> QueryEmbeddings,
        [property: JsonPropertyName("n_results")] int NResults,
        [property: JsonPropertyName("include")] List<string> Include);

    private record QueryResponse(
        [property: JsonPropertyName("documents")] List<List<string?>> Documents,
        [property: JsonPropertyName("metadatas")] List<List<Dictionary<string, string>?>> Metadatas,
        [property: JsonPropertyName("distances")] List<List<float>> Distances);
}
