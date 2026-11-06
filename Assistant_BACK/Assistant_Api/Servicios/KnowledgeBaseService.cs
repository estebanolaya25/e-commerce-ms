using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Assistant_Api.Modelos;
using Microsoft.Extensions.Options;

namespace Assistant_Api.Servicios;

/// <summary>Indexa los documentos de la tienda en ChromaDB y busca los fragmentos relevantes.</summary>
public class KnowledgeBaseService(
    ChromaService chroma,
    EmbeddingService embeddings,
    IOptions<RagOptions> options,
    IWebHostEnvironment env,
    ILogger<KnowledgeBaseService> logger)
{
    private static readonly string[] SupportedExtensions = [".md", ".txt"];
    private readonly RagOptions _options = options.Value;

    public string DocumentsRoot => Path.GetFullPath(Path.Combine(env.ContentRootPath, _options.DocumentsPath));

    public async Task<ResultadoIndexacion> ReindexAsync(CancellationToken ct)
    {
        if (!Directory.Exists(DocumentsRoot))
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de documentos: {DocumentsRoot}");
        }

        await chroma.DeleteCollectionAsync(ct);
        var collectionId = await chroma.GetOrCreateCollectionAsync(ct);

        var files = Directory.EnumerateFiles(DocumentsRoot, "*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => !_options.ExcludedFiles.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        var total = 0;
        foreach (var file in files)
        {
            var source = Path.GetRelativePath(DocumentsRoot, file).Replace('\\', '/');
            var chunks = ChunkMarkdown(QuitarComentarios(await File.ReadAllTextAsync(file, ct)), source);
            if (chunks.Count == 0)
            {
                continue;
            }

            var vectors = await embeddings.EmbedDocumentsAsync(chunks.Select(c => c.Text), ct);
            await chroma.AddAsync(collectionId, chunks, vectors, ct);
            total += chunks.Count;
            logger.LogInformation("Indexado {Source}: {Count} fragmentos", source, chunks.Count);
        }

        return new ResultadoIndexacion(files.Count, total);
    }

    public async Task<int> CountAsync(CancellationToken ct) =>
        await chroma.CountAsync(await chroma.GetOrCreateCollectionAsync(ct), ct);

    /// <summary>
    /// Busca con cada consulta (ampliada con sinónimos) y combina los resultados conservando la menor distancia de
    /// cada fragmento, para que una consulta ruidosa no desplace lo que otra sí encontró.
    /// </summary>
    public async Task<List<ChromaResult>> SearchAsync(IEnumerable<string> queries, CancellationToken ct)
    {
        var collectionId = await chroma.GetOrCreateCollectionAsync(ct);
        var best = new Dictionary<string, ChromaResult>();
        foreach (var query in queries.Where(q => !string.IsNullOrWhiteSpace(q)).Select(ExpandWithSynonyms).Distinct())
        {
            var vector = await embeddings.EmbedQueryAsync(query, ct);
            foreach (var result in await chroma.QueryAsync(collectionId, vector, _options.TopK, ct))
            {
                if (!best.TryGetValue(result.Text, out var current) || result.Distance < current.Distance)
                {
                    best[result.Text] = result;
                }
            }
        }
        return best.Values.OrderBy(r => r.Distance).Take(_options.TopK).ToList();
    }

    /// <summary>Grupos de sinónimos con al menos un término presente en el texto.</summary>
    public List<List<string>> MatchingSynonymGroups(string text)
    {
        var normalized = Normalize(text);
        return _options.Sinonimos.Where(group => group.Any(term => ContainsTerm(normalized, term))).ToList();
    }

    private string ExpandWithSynonyms(string query)
    {
        var extra = RelatedTerms(query);
        return extra.Count == 0 ? query : $"{query}\n({string.Join(", ", extra)})";
    }

    /// <summary>Términos de los grupos de sinónimos presentes en el texto que el texto aún no contiene.</summary>
    private List<string> RelatedTerms(string text)
    {
        var normalized = Normalize(text);
        return MatchingSynonymGroups(text)
            .SelectMany(group => group)
            .Where(term => !ContainsTerm(normalized, term))
            .Distinct()
            .ToList();
    }

    /// <summary>El término debe empezar en inicio de palabra: "sede" encuentra "sedes" pero no "presede".</summary>
    private static bool ContainsTerm(string normalizedText, string term) =>
        Regex.IsMatch(normalizedText, $@"\b{Regex.Escape(Normalize(term))}");

    /// <summary>Minúsculas y sin tildes, para que "dirección" y "direccion" coincidan.</summary>
    private static string Normalize(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// Divide un markdown por secciones "## ". Cada fragmento lleva el título del documento y de la sección
    /// para que conserve el contexto; las secciones largas se parten por párrafos.
    /// </summary>
    private List<ChromaDocument> ChunkMarkdown(string content, string source)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var title = lines.FirstOrDefault(l => l.StartsWith("# "))?[2..].Trim() ?? Path.GetFileNameWithoutExtension(source);

        var sections = new List<(string Heading, string Body)>();
        var heading = string.Empty;
        var body = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.StartsWith("# "))
            {
                continue;
            }
            if (line.StartsWith("## "))
            {
                sections.Add((heading, body.ToString()));
                heading = line[3..].Trim();
                body.Clear();
                continue;
            }
            body.AppendLine(line);
        }
        sections.Add((heading, body.ToString()));

        var chunks = new List<ChromaDocument>();
        foreach (var (sectionHeading, sectionBody) in sections)
        {
            foreach (var piece in SplitByParagraphs(TablesToSentences(sectionBody).Trim(), _options.MaxChunkChars))
            {
                var header = string.IsNullOrEmpty(sectionHeading)
                    ? $"Documento: {title}"
                    : $"Documento: {title}\nSección: {sectionHeading}{SectionAliases(sectionHeading)}";
                chunks.Add(new ChromaDocument(
                    $"{source}#{chunks.Count}",
                    $"{header}\n\n{piece}",
                    new Dictionary<string, string>
                    {
                        ["source"] = source,
                        ["title"] = title,
                        ["section"] = sectionHeading
                    }));
            }
        }
        return chunks;
    }

    /// <summary>
    /// Sinónimos del título de la sección, para que el fragmento también se encuentre con las palabras del usuario
    /// ("Oficina principal" → "sede principal, casa matriz…"). Solo se mira el título, no el cuerpo, para no
    /// enriquecer fragmentos que apenas mencionan el término (p. ej. "Sede: Bogotá" en el directorio).
    /// </summary>
    private string SectionAliases(string heading)
    {
        var aliases = RelatedTerms(heading);
        return aliases.Count == 0 ? string.Empty : $"\nTambién conocido como: {string.Join(", ", aliases)}";
    }

    /// <summary>
    /// Convierte cada fila de una tabla markdown en una línea "Encabezado: valor; Encabezado: valor." Los modelos
    /// pequeños confunden las filas al leer tablas (p. ej. daban la dirección de Barranquilla para Cali).
    /// </summary>
    private static string TablesToSentences(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var result = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            var isTableStart = lines[i].TrimStart().StartsWith('|')
                && i + 1 < lines.Length
                && TableSeparator.IsMatch(lines[i + 1]);
            if (!isTableStart)
            {
                result.AppendLine(lines[i]);
                continue;
            }

            var headers = TableCells(lines[i]);
            for (i += 2; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++)
            {
                var cells = TableCells(lines[i]);
                var pairs = headers.Zip(cells, (header, cell) => $"{header}: {cell}").Where(p => !p.EndsWith(": "));
                var sentence = string.Join("; ", pairs);
                result.AppendLine("- " + (sentence.EndsWith('.') ? sentence : sentence + "."));
            }
            i--;
        }
        return result.ToString();
    }

    /// <summary>
    /// Quita los comentarios HTML (&lt;!-- ... --&gt;): sirven para notas internas en los documentos (p. ej. "revisar
    /// con el área legal") que no deben llegar al modelo ni al cliente.
    /// </summary>
    private static string QuitarComentarios(string text) => ComentarioHtml.Replace(text, string.Empty);

    private static readonly Regex ComentarioHtml = new(@"<!--.*?-->", RegexOptions.Singleline);

    private static readonly Regex TableSeparator = new(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$");

    private static List<string> TableCells(string row) =>
        row.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();

    private static IEnumerable<string> SplitByParagraphs(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }
        if (text.Length <= maxChars)
        {
            yield return text;
            yield break;
        }

        var current = new StringBuilder();
        foreach (var paragraph in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + paragraph.Length > maxChars)
            {
                yield return current.ToString().Trim();
                current.Clear();
            }
            current.AppendLine(paragraph).AppendLine();
        }
        if (current.Length > 0)
        {
            yield return current.ToString().Trim();
        }
    }
}
