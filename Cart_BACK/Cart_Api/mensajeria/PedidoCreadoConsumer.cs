using CapaNegocios.Interfaz.carrito.interfaz;
using Ecommerce.Contratos.Pedidos;
using MassTransit;

namespace Cart_Api.mensajeria
{
    // Coreografía: Pedidos no llama a Carrito; solo anuncia "PedidoCreado" y Carrito reacciona por su cuenta.
    public class PedidoCreadoConsumer : IConsumer<PedidoCreado>
    {
        private readonly ICarritoCapaNegocios _carritoCapaNegocios;
        private readonly ILogger<PedidoCreadoConsumer> _logger;

        public PedidoCreadoConsumer(ICarritoCapaNegocios carritoCapaNegocios, ILogger<PedidoCreadoConsumer> logger)
        {
            _carritoCapaNegocios = carritoCapaNegocios;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PedidoCreado> context)
        {
            PedidoCreado pedido = context.Message;
            long quitados = await _carritoCapaNegocios.QuitarComprados(pedido.UsuarioId, pedido.Items.Select(i => i.Sku));
            _logger.LogInformation("Pedido {Numero}: se quitaron {Quitados} productos comprados del carrito del usuario {UsuarioId}.",
                pedido.Numero, quitados, pedido.UsuarioId);
        }
    }
}
