using CapaNegocios.Interfaz.pedidos.interfaz;
using Ecommerce.Contratos.Pagos;
using MassTransit;

namespace Orders_Api.mensajeria
{
    // Los consumidores solo traducen el mensaje de RabbitMQ y delegan en la capa de negocios.
    // El MessageId llega igual en cada reentrega: con él la base de datos descarta los duplicados.
    public class PagoAprobadoConsumer : IConsumer<PagoAprobado>
    {
        private readonly ISagaPedidosCapaNegocios _saga;

        public PagoAprobadoConsumer(ISagaPedidosCapaNegocios saga) => _saga = saga;

        public Task Consume(ConsumeContext<PagoAprobado> context) =>
            _saga.ProcesarPagoAprobado(context.MessageId ?? throw new InvalidOperationException("Mensaje sin MessageId."), context.Message);
    }

    public class PagoRechazadoConsumer : IConsumer<PagoRechazado>
    {
        private readonly ISagaPedidosCapaNegocios _saga;

        public PagoRechazadoConsumer(ISagaPedidosCapaNegocios saga) => _saga = saga;

        public Task Consume(ConsumeContext<PagoRechazado> context) =>
            _saga.ProcesarPagoRechazado(context.MessageId ?? throw new InvalidOperationException("Mensaje sin MessageId."), context.Message);
    }

    public class PagoReembolsadoConsumer : IConsumer<PagoReembolsado>
    {
        private readonly ISagaPedidosCapaNegocios _saga;

        public PagoReembolsadoConsumer(ISagaPedidosCapaNegocios saga) => _saga = saga;

        public Task Consume(ConsumeContext<PagoReembolsado> context) =>
            _saga.ProcesarPagoReembolsado(context.MessageId ?? throw new InvalidOperationException("Mensaje sin MessageId."), context.Message);
    }
}
