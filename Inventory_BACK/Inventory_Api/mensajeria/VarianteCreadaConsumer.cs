using CapaNegocios.Interfaz.stock.interfaz;
using Ecommerce.Contratos.Catalogo;
using MassTransit;

namespace Inventory_Api.mensajeria
{
    public class VarianteCreadaConsumer : IConsumer<VarianteCreada>
    {
        private readonly IStockCapaNegocios _stockCapaNegocios;

        public VarianteCreadaConsumer(IStockCapaNegocios stockCapaNegocios)
        {
            _stockCapaNegocios = stockCapaNegocios;
        }

        public async Task Consume(ConsumeContext<VarianteCreada> context)
        {
            Guid mensajeId = context.MessageId ?? throw new InvalidOperationException("El mensaje no trae MessageId.");
            VarianteCreada evento = context.Message;
            await _stockCapaNegocios.RegistrarDesdeEvento(mensajeId, evento.Sku, evento.ProductoId, evento.VarianteId);
        }
    }
}
