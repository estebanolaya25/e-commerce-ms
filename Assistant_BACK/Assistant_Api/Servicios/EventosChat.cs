using System.Text.Json;
using Assistant_Api.Modelos;

namespace Assistant_Api.Servicios;

/// <summary>Recibe los eventos de una respuesta en curso (para transmitirlos al navegador).</summary>
public delegate Task EmitirEvento(EventoChat evento);

/// <summary>
/// Evento de una respuesta transmitida por SSE:
/// "estado" (qué está consultando), "texto" (un fragmento de la respuesta), "fin" (la respuesta completa, con productos
/// y acciones) o "error" (no se pudo responder; la conexión ya estaba abierta, así que no hay código HTTP de error).
/// </summary>
public record EventoChat(string Tipo, string? Texto = null, ChatResponse? Respuesta = null, string? Mensaje = null)
{
    public static EventoChat Estado(string texto) => new("estado", Texto: texto);
    public static EventoChat Fragmento(string texto) => new("texto", Texto: texto);
    public static EventoChat Fin(ChatResponse respuesta) => new("fin", Respuesta: respuesta);
    public static EventoChat Error(string mensaje) => new("error", Mensaje: mensaje);
}

/// <summary>Escribe una respuesta del asistente como Server-Sent Events y traduce los errores a eventos "error".</summary>
public static class TransmisionSse
{
    public static async Task TransmitirAsync(HttpResponse response, Func<EmitirEvento, Task<ChatResponse>> responder,
        ILogger logger, CancellationToken ct)
    {
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        // Evita que un proxy intermedio (nginx, por ejemplo) acumule la respuesta antes de enviarla.
        response.Headers["X-Accel-Buffering"] = "no";
        await response.StartAsync(ct);

        async Task Emitir(EventoChat evento)
        {
            await response.WriteAsync($"data: {JsonSerializer.Serialize(evento, JsonSerializerOptions.Web)}\n\n", ct);
            await response.Body.FlushAsync(ct);
        }

        try
        {
            var respuesta = await responder(Emitir);
            await Emitir(EventoChat.Fin(respuesta));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El navegador cerró la conexión: no hay a quién avisar.
        }
        catch (AsistenteOcupadoException)
        {
            await Emitir(EventoChat.Error("El asistente está atendiendo muchas consultas. Intente de nuevo en unos segundos."));
        }
        catch (Exception ex) when (Fallos.EsTransitorio(ex))
        {
            logger.LogError(ex, "Error en una respuesta transmitida del asistente");
            await Emitir(EventoChat.Error("El asistente no está disponible en este momento."));
        }
    }
}
