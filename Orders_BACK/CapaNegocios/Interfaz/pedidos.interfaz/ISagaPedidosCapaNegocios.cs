using Ecommerce.Contratos.Pagos;

namespace CapaNegocios.Interfaz.pedidos.interfaz
{
    // Reacciones de Pedidos a los eventos de Pagos y al paso del tiempo (pasos de la saga del checkout).
    public interface ISagaPedidosCapaNegocios
    {
        Task ProcesarPagoAprobado(Guid mensajeId, PagoAprobado evento);
        Task ProcesarPagoRechazado(Guid mensajeId, PagoRechazado evento);
        Task ProcesarPagoReembolsado(Guid mensajeId, PagoReembolsado evento);
        Task<int> ExpirarVencidos();
    }
}
