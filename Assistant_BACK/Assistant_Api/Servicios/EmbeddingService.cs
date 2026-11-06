using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Assistant_Api.Servicios;

/// <summary>Genera embeddings con Ollama (nomic-embed-text).</summary>
public class EmbeddingService(HttpClient http, IOptions<RagOptions> options)
{
    private readonly RagOptions _options = options.Value;

    // nomic-embed-text da mejores resultados con estos prefijos de tarea.
    public Task<List<float[]>> EmbedDocumentsAsync(IEnumerable<string> texts, CancellationToken ct) =>
        EmbedAsync(texts.Select(t => "search_document: " + t).ToList(), ct);

    public async Task<float[]> EmbedQueryAsync(string text, CancellationToken ct) =>
        (await EmbedAsync(["search_query: " + text], ct))[0];

    private async Task<List<float[]>> EmbedAsync(List<string> input, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/api/embed", new EmbedRequest(_options.EmbeddingModel, input), ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Ollama (embed) respondió {(int)response.StatusCode}: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(ct)
            ?? throw new InvalidOperationException("Respuesta vacía de Ollama (embed).");
        return result.Embeddings;
    }

    private record EmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] List<string> Input);

    private record EmbedResponse(
        [property: JsonPropertyName("embeddings")] List<float[]> Embeddings);
}
