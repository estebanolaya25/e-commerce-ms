namespace Assistant_Api.Servicios;

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen3.5:latest";
    public string? SystemPrompt { get; set; }

    /// <summary>Instrucciones del asistente del panel interno (/admin), para el personal de la tienda.</summary>
    public string? SystemPromptPanel { get; set; }
    public double Temperature { get; set; } = 0.2;
    public int ContextSize { get; set; } = 8192;

    /// <summary>
    /// Razonamiento previo de los modelos que lo soportan (qwen3.5). Apagado responde mucho más rápido;
    /// null no lo envía y deja el comportamiento por defecto del modelo.
    /// </summary>
    public bool? Think { get; set; } = false;

    /// <summary>
    /// Cuánto mantiene Ollama el modelo en memoria tras la última petición (p. ej. "30m"; "-1" = siempre). Por defecto
    /// Ollama lo descarga a los 5 minutos y la siguiente pregunta espera varios segundos a que vuelva a cargar.
    /// </summary>
    public string? KeepAlive { get; set; } = "30m";

    /// <summary>Respuestas que se generan a la vez. El resto espera turno para no saturar la GPU.</summary>
    public int MaxConcurrencia { get; set; } = 2;

    /// <summary>Tiempo máximo esperando turno antes de responder 503.</summary>
    public int SegundosEsperaTurno { get; set; } = 30;
}

public class RagOptions
{
    public string ChromaUrl { get; set; } = "http://localhost:8000";
    public string Collection { get; set; } = "tienda_andina";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string DocumentsPath { get; set; } = "BaseConocimiento";
    public List<string> ExcludedFiles { get; set; } = [];
    public int TopK { get; set; } = 5;
    public int MaxChunkChars { get; set; } = 1500;

    /// <summary>Si la colección está vacía al arrancar, se indexan los documentos automáticamente.</summary>
    public bool IndexarAlIniciar { get; set; } = true;

    /// <summary>
    /// Grupos de términos que significan lo mismo en la tienda (p. ej. "envío" = "despacho").
    /// Si una pregunta contiene un término, la búsqueda se amplía con los demás del grupo.
    /// </summary>
    public List<List<string>> Sinonimos { get; set; } = [];
}

public class ProductosOptions
{
    /// <summary>Colección de ChromaDB con un documento por producto (aparte de la base de conocimiento).</summary>
    public string Coleccion { get; set; } = "productos_tienda";

    /// <summary>Productos candidatos que se pasan al modelo en cada pregunta.</summary>
    public int Candidatos { get; set; } = 6;

    /// <summary>Si la pregunta trae un rango de precio, se buscan más vecinos para que queden suficientes tras filtrar.</summary>
    public int CandidatosConFiltroPrecio { get; set; } = 60;

    /// <summary>Distancia coseno máxima: por encima, el producto no tiene relación con la pregunta y no se ofrece.</summary>
    public float DistanciaMaxima { get; set; } = 0.36f;

    /// <summary>Tarjetas de producto que puede mostrar una respuesta.</summary>
    public int MaxSugerencias { get; set; } = 4;

    /// <summary>Productos que se leen de Catálogo en paralelo durante una sincronización completa.</summary>
    public int ParalelismoSincronizacion { get; set; } = 4;
}

public class AsistenteOptions
{
    /// <summary>Mensajes del historial que se envían al modelo (los más recientes).</summary>
    public int MaxMensajes { get; set; } = 12;

    /// <summary>Longitud máxima de cada mensaje del usuario.</summary>
    public int MaxCaracteresMensaje { get; set; } = 1000;
}
