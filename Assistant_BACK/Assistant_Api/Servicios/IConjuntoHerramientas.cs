using System.Text.Json;

namespace Assistant_Api.Servicios;

/// <summary>Herramientas que el modelo puede pedir en una conversación (las del cliente o las del panel interno).</summary>
public interface IConjuntoHerramientas
{
    /// <summary>Definiciones en el formato de Ollama, solo las que el usuario puede usar según sus roles.</summary>
    JsonElement DefinicionesPara(SesionHerramientas sesion);

    /// <summary>Ejecuta una herramienta y devuelve el resultado como JSON para el modelo. No lanza por errores de datos.</summary>
    Task<string> EjecutarAsync(string nombre, JsonElement argumentos, SesionHerramientas sesion, CancellationToken ct);
}
