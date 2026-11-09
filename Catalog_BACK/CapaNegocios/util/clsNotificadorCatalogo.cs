using CapaDatos.Interfaz.productos.interfaz;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.eventos.interfaz;
using Ecommerce.Contratos.Catalogo;

namespace CapaNegocios.util
{
    // Avisa por RabbitMQ que un producto (o el catálogo en general) cambió, para que otros servicios
    // —hoy el Asistente, que mantiene su índice de búsqueda— se actualicen sin consultar la base de Catálogo.
    // Igual que el resto de eventos de Catálogo, un fallo al publicar se registra y no revierte el cambio.
    public class clsNotificadorCatalogo
    {
        private readonly IProductosCapaDatos _productosCapaDatos;
        private readonly IPublicadorEventos _publicadorEventos;

        public clsNotificadorCatalogo(IProductosCapaDatos productosCapaDatos, IPublicadorEventos publicadorEventos)
        {
            _productosCapaDatos = productosCapaDatos;
            _publicadorEventos = publicadorEventos;
        }

        public async Task ProductoModificado(Guid productoId)
        {
            // Se lee el slug actual (incluye inactivos): el consumidor lo usa para volver a pedir el producto.
            ProductoDetalleDto? producto = await _productosCapaDatos.ObtenerProductoPorId(productoId, true);
            if (producto == null)
                return;

            await _publicadorEventos.Publicar(new ProductoModificado
            {
                ProductoId = producto.Id,
                Slug = producto.Slug,
                ModificadoEn = DateTime.UtcNow
            });
        }

        public Task CatalogoModificado(string motivo)
        {
            return _publicadorEventos.Publicar(new CatalogoModificado
            {
                Motivo = motivo,
                ModificadoEn = DateTime.UtcNow
            });
        }
    }
}
