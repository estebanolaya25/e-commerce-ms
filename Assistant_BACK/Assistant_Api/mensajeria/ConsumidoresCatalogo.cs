using Assistant_Api.Servicios;
using Ecommerce.Contratos.Catalogo;
using MassTransit;

namespace Assistant_Api.mensajeria
{
    // Mantiene el índice de productos al día con Catálogo. Los dos consumidores son idempotentes por diseño: vuelven a
    // leer el estado actual del producto (o del catálogo), así que un mensaje repetido o fuera de orden no deja datos viejos.
    public class ProductoModificadoConsumer : IConsumer<ProductoModificado>
    {
        private readonly ProductosIndiceService _indice;

        public ProductoModificadoConsumer(ProductosIndiceService indice)
        {
            _indice = indice;
        }

        public Task Consume(ConsumeContext<ProductoModificado> context) =>
            _indice.ActualizarProductoAsync(context.Message.ProductoId, context.Message.Slug, context.CancellationToken);
    }

    // Cambió una categoría o marca: puede afectar a cientos de productos, así que se sincroniza todo.
    public class CatalogoModificadoConsumer : IConsumer<CatalogoModificado>
    {
        private readonly ProductosIndiceService _indice;
        private readonly ILogger<CatalogoModificadoConsumer> _logger;

        public CatalogoModificadoConsumer(ProductosIndiceService indice, ILogger<CatalogoModificadoConsumer> logger)
        {
            _indice = indice;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CatalogoModificado> context)
        {
            _logger.LogInformation("Catálogo modificado ({Motivo}): sincronizando productos", context.Message.Motivo);
            await _indice.SincronizarTodoAsync(context.CancellationToken);
        }
    }
}
