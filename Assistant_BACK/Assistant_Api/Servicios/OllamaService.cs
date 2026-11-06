using System.Text.Json.Serialization;
using Assistant_Api.Modelos;
using Microsoft.Extensions.Options;

namespace Assistant_Api.Servicios;

/// <summary>El modelo está ocupado con otras respuestas y no hubo turno a tiempo.</summary>
public class AsistenteOcupadoException() : Exception("El asistente está atendiendo muchas consultas.");

public class OllamaService(
    HttpClient http,
    IOptions<OllamaOptions> options,
    IOptions<AsistenteOptions> asistenteOptions,
    KnowledgeBaseService knowledgeBase,
    ColaOllama cola)
{
    private readonly OllamaOptions _options = options.Value;
    private readonly AsistenteOptions _asistente = asistenteOptions.Value;

    public async Task<ChatResponse> ChatAsync(IReadOnlyList<MensajeChat> historial, CancellationToken ct)
    {
        // Se busca con cada una de las dos últimas preguntas y con ambas juntas, para soportar preguntas de
        // seguimiento ("¿y si es a Medellín?") sin que el ruido de una desplace lo que la otra sí encuentra.
        var lastQuestions = historial
            .Where(m => m.Role == "user")
            .TakeLast(2)
            .Select(m => m.Content)
            .ToList();
        var combinedQuestions = string.Join("\n", lastQuestions);
        var fragments = await knowledgeBase.SearchAsync([.. lastQuestions, combinedQuestions], ct);
        var synonyms = knowledgeBase.MatchingSynonymGroups(combinedQuestions);

        var messages = new List<OllamaMessage> { new("system", BuildSystemPrompt(fragments, synonyms)) };
        messages.AddRange(historial.Select(m => new OllamaMessage(m.Role, m.Content)));

        var payload = new OllamaChatRequest(_options.Model, messages, Stream: false, _options.Think,
            new OllamaChatOptions(_options.Temperature, _options.ContextSize));

        if (!await cola.EsperarTurnoAsync(TimeSpan.FromSeconds(_options.SegundosEsperaTurno), ct))
        {
            throw new AsistenteOcupadoException();
        }

        OllamaChatResponse? result;
        try
        {
            using var response = await http.PostAsJsonAsync("/api/chat", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Ollama respondió {(int)response.StatusCode}: {error}");
            }
            result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(ct);
        }
        finally
        {
            cola.LiberarTurno();
        }

        var sources = fragments
            .Select(f => f.Metadata.GetValueOrDefault("title", string.Empty))
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();

        return new ChatResponse(result?.Message?.Content?.Trim() ?? string.Empty, sources);
    }

    /// <summary>
    /// Deja solo los mensajes de usuario y asistente (un cliente no puede inyectar mensajes "system"), los más
    /// recientes y recortados. Devuelve null si el último mensaje no es una pregunta del usuario.
    /// </summary>
    public List<MensajeChat>? NormalizarHistorial(IEnumerable<MensajeChat>? mensajes)
    {
        var historial = (mensajes ?? [])
            .Where(m => m is not null && (m.Role == "user" || m.Role == "assistant") && !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(_asistente.MaxMensajes)
            .Select(m => m with
            {
                Content = m.Content.Length > _asistente.MaxCaracteresMensaje
                    ? m.Content[.._asistente.MaxCaracteresMensaje]
                    : m.Content
            })
            .ToList();

        return historial.Count > 0 && historial[^1].Role == "user" ? historial : null;
    }

    private string BuildSystemPrompt(List<ChromaResult> fragments, List<List<string>> synonyms)
    {
        var context = fragments.Count == 0
            ? "(No se encontró información relevante en la base de conocimiento.)"
            : string.Join("\n\n---\n\n", fragments.Select(f => f.Text));

        // Solo se incluyen los grupos que aparecen en la pregunta, para que el modelo trate como equivalentes las
        // palabras del usuario y las del documento ("despacho" = "envío").
        var glossary = synonyms.Count == 0
            ? string.Empty
            : "TÉRMINOS EQUIVALENTES (en esta tienda significan lo mismo):\n"
              + string.Join("\n", synonyms.Select(g => "- " + string.Join(" = ", g.Select(t => $"\"{t}\""))))
              + "\n\n";

        return $"""
            {_options.SystemPrompt}

            {glossary}INFORMACIÓN DE LA TIENDA (única fuente válida para responder):
            <contexto>
            {context}
            </contexto>
            """;
    }

    private record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("think"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Think,
        [property: JsonPropertyName("options")] OllamaChatOptions Options);

    private record OllamaChatOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("num_ctx")] int NumCtx);

    private record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message);
}

/// <summary>
/// Limita cuántas respuestas genera Ollama a la vez (singleton). Con un modelo local, muchas peticiones simultáneas
/// solo hacen que todas tarden más; es mejor que esperen turno y, si la espera es larga, responder 503.
/// </summary>
public class ColaOllama(IOptions<OllamaOptions> options)
{
    private readonly SemaphoreSlim _turnos = new(Math.Max(1, options.Value.MaxConcurrencia));

    public Task<bool> EsperarTurnoAsync(TimeSpan espera, CancellationToken ct) => _turnos.WaitAsync(espera, ct);

    public void LiberarTurno() => _turnos.Release();
}
