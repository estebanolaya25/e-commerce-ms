using System.Globalization;
using System.Text;
using System.Text.Json;
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
    IOptions<ProductosOptions> productosOptions,
    KnowledgeBaseService knowledgeBase,
    ProductosIndiceService productos,
    ColaOllama cola,
    ILogger<OllamaService> logger)
{
    private readonly OllamaOptions _options = options.Value;
    private readonly AsistenteOptions _asistente = asistenteOptions.Value;
    private readonly ProductosOptions _productos = productosOptions.Value;

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    // Salida estructurada de Ollama: el modelo devuelve el texto y, aparte, los slugs de los productos que recomienda.
    // Así las tarjetas solo aparecen cuando el modelo decidió que la pregunta era sobre productos.
    private static readonly JsonElement FormatoRespuesta = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "respuesta": { "type": "string" },
            "productos": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["respuesta", "productos"]
        }
        """).RootElement;

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
        var candidatos = await BuscarProductosAsync(lastQuestions, combinedQuestions, ct);

        var messages = new List<OllamaMessage> { new("system", BuildSystemPrompt(fragments, synonyms, candidatos)) };
        messages.AddRange(historial.Select(m => new OllamaMessage(m.Role, m.Content)));

        var payload = new OllamaChatRequest(_options.Model, messages, Stream: false, _options.Think,
            new OllamaChatOptions(_options.Temperature, _options.ContextSize), FormatoRespuesta);

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

        var (respuesta, slugs) = LeerSalida(result?.Message?.Content);
        var sugeridos = slugs
            .Select(slug => candidatos.FirstOrDefault(c => c.Slug.Equals(slug.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Where(c => c is not null)
            .DistinctBy(c => c!.Slug)
            .Take(_productos.MaxSugerencias)
            .Select(c => c!.ATarjeta())
            .ToList();

        return new ChatResponse(respuesta, sources, sugeridos);
    }

    /// <summary>
    /// Busca con las dos últimas preguntas juntas para entender seguimientos ("¿y en negro?"). El rango de precio se
    /// toma de la última pregunta y, si no trae, del contexto. Un fallo aquí no impide responder lo demás.
    /// </summary>
    private async Task<List<ProductoCandidato>> BuscarProductosAsync(List<string> lastQuestions, string combined, CancellationToken ct)
    {
        try
        {
            var filtro = FiltroPrecio.Extraer(lastQuestions.LastOrDefault() ?? string.Empty);
            if (filtro.Vacio)
            {
                filtro = FiltroPrecio.Extraer(combined);
            }
            return await productos.BuscarAsync(combined, filtro, ct);
        }
        catch (Exception ex) when (Fallos.EsTransitorio(ex) && !ct.IsCancellationRequested)
        {
            logger.LogWarning("No se pudieron buscar productos: {Mensaje}", ex.Message);
            return [];
        }
    }

    /// <summary>Lee el JSON del modelo. Si no viene bien formado, se usa el texto tal cual y sin productos.</summary>
    private static (string Respuesta, List<string> Slugs) LeerSalida(string? contenido)
    {
        var texto = contenido?.Trim() ?? string.Empty;
        try
        {
            var salida = JsonSerializer.Deserialize<SalidaModelo>(texto);
            if (salida?.Respuesta is { Length: > 0 })
            {
                return (salida.Respuesta.Trim(), salida.Productos ?? []);
            }
        }
        catch (JsonException)
        {
        }
        return (texto, []);
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

    private string BuildSystemPrompt(List<ChromaResult> fragments, List<List<string>> synonyms, List<ProductoCandidato> candidatos)
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

            PRODUCTOS DEL CATÁLOGO RELACIONADOS CON LA PREGUNTA (precios y disponibilidad de este momento):
            <productos>
            {DescribirProductos(candidatos)}
            </productos>

            FORMATO DE RESPUESTA: responde SOLO con un objeto JSON {EjemploSalida}.
            - "respuesta": tu mensaje para el cliente (puede usar **negritas** y viñetas con "- ").
            - "productos": si el cliente busca, compara o pide recomendaciones de productos, incluye los slugs (máximo {_productos.MaxSugerencias}) de los productos de <productos> que mejor encajan, empezando por los disponibles. Si la pregunta no es sobre productos, o ninguno encaja, deja la lista vacía.
            - Solo puedes recomendar productos que estén en <productos>, con su precio exacto. Nunca inventes productos, precios, tallas, colores ni existencias.
            - Si el cliente pide un precio máximo o mínimo, compara cada precio antes de recomendar: solo incluye los que cumplen. Si ninguno cumple, dilo con claridad y no ofrezcas los demás como si cumplieran.
            - Si recomiendas productos, menciónalos brevemente en "respuesta" (nombre y precio); la tienda mostrará sus tarjetas debajo del mensaje.
            - Si buscan un producto y <productos> está vacío o no encaja, dilo y sugiere usar el buscador de la tienda.
            """;
    }

    private const string EjemploSalida = """{"respuesta": "...", "productos": ["slug", ...]}""";

    private static string DescribirProductos(List<ProductoCandidato> candidatos)
    {
        if (candidatos.Count == 0)
        {
            return "(Ningún producto del catálogo se relaciona con la pregunta.)";
        }

        var texto = new StringBuilder();
        foreach (var c in candidatos)
        {
            texto.Append($"- slug: {c.Slug} | {c.Nombre}");
            if (c.Marca is not null) texto.Append($" | marca: {c.Marca}");
            texto.Append($" | categoría: {c.Categoria} | precio: {c.Precio.ToString("C0", Colombia)}");
            if (c.PrecioAntes is not null) texto.Append($" (antes {c.PrecioAntes.Value.ToString("C0", Colombia)})");
            if (c.Colores.Length > 0) texto.Append($" | colores: {c.Colores}");
            if (c.Tallas.Length > 0) texto.Append($" | tallas: {c.Tallas}");
            texto.Append(c.Disponible switch
            {
                true => " | disponible",
                false => " | AGOTADO",
                null => " | disponibilidad sin confirmar"
            });
            texto.AppendLine();
        }
        return texto.ToString().TrimEnd();
    }

    private record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("think"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Think,
        [property: JsonPropertyName("options")] OllamaChatOptions Options,
        [property: JsonPropertyName("format")] JsonElement Format);

    private record OllamaChatOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("num_ctx")] int NumCtx);

    private record SalidaModelo(
        [property: JsonPropertyName("respuesta")] string? Respuesta,
        [property: JsonPropertyName("productos")] List<string>? Productos);

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
