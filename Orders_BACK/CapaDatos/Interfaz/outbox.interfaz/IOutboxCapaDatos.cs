using CapaDto.Modelos;

namespace CapaDatos.Interfaz.outbox.interfaz
{
    public interface IOutboxCapaDatos
    {
        // Toma los eventos pendientes (bloqueándolos para que otra instancia no los publique a la vez),
        // llama a publicar para cada uno y los marca como publicados. Si uno falla, se detiene para conservar el orden.
        Task<int> ProcesarPendientes(int limite, Func<MensajeOutbox, Task> publicar);
    }
}
