using CapaDatos.Interfaz.carrito.interfaz;
using CapaDatos.Interfaz.servicios.interfaz;
using CapaDto.Externos;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.carrito.interfaz;
using Microsoft.Extensions.Configuration;

namespace CapaNegocios.Implementacion.carrito.Implementacion
{
    public class clsCarritoCapaNegocios : ICarritoCapaNegocios
    {
        private readonly ICarritoCapaDatos _carritoCapaDatos;
        private readonly ICatalogoCliente _catalogoCliente;
        private readonly IInventarioCliente _inventarioCliente;
        private readonly IConfiguration _configuration;

        public clsCarritoCapaNegocios(
            ICarritoCapaDatos carritoCapaDatos,
            ICatalogoCliente catalogoCliente,
            IInventarioCliente inventarioCliente,
            IConfiguration configuration)
        {
            _carritoCapaDatos = carritoCapaDatos;
            _catalogoCliente = catalogoCliente;
            _inventarioCliente = inventarioCliente;
            _configuration = configuration;
        }

        private int MaxProductos => int.TryParse(_configuration["Carrito:MaxProductos"], out int valor) ? valor : 50;
        private int MaxUnidadesPorProducto => int.TryParse(_configuration["Carrito:MaxUnidadesPorProducto"], out int valor) ? valor : 10;
        private TimeSpan Expiracion => TimeSpan.FromDays(int.TryParse(_configuration["Carrito:DiasExpiracion"], out int valor) ? valor : 30);

        public async Task<CarritoDto> Obtener(Guid usuarioId)
        {
            Dictionary<string, int> items = await _carritoCapaDatos.ObtenerItems(usuarioId);
            var carrito = new CarritoDto();
            if (items.Count == 0)
                return carrito;

            // Las dos consultas salen al mismo tiempo: el carrito tarda lo que tarde el servicio más lento, no la suma.
            Task<List<VarianteCatalogoDto>?> tareaCatalogo = _catalogoCliente.ObtenerVariantes(items.Keys);
            Task<List<DisponibilidadInventarioDto>?> tareaInventario = _inventarioCliente.ObtenerDisponibilidad(items.Keys);
            await Task.WhenAll(tareaCatalogo, tareaInventario);

            Dictionary<string, VarianteCatalogoDto>? variantes = (await tareaCatalogo)?.ToDictionary(v => v.Sku, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, DisponibilidadInventarioDto>? stock = (await tareaInventario)?.ToDictionary(d => d.Sku, StringComparer.OrdinalIgnoreCase);

            if (variantes == null)
                carrito.Avisos.Add("No se pudo consultar el catálogo: los nombres y precios no están disponibles en este momento.");
            if (stock == null)
                carrito.Avisos.Add("No se pudo consultar el inventario: la disponibilidad no está confirmada.");
            carrito.InformacionCompleta = variantes != null && stock != null;

            foreach (var (sku, cantidad) in items)
                carrito.Items.Add(ConstruirItem(sku, cantidad, variantes, stock));

            carrito.Items = carrito.Items.OrderBy(i => i.Nombre ?? i.Sku).ThenBy(i => i.Sku).ToList();
            carrito.ProductosDistintos = carrito.Items.Count;
            carrito.Unidades = carrito.Items.Sum(i => i.Cantidad);
            carrito.Total = carrito.Items.Where(CuentaEnTotal).Sum(i => i.Subtotal ?? 0);
            carrito.PuedeComprar = carrito.InformacionCompleta
                && carrito.Items.All(i => i.Estado is EstadoItem.Disponible or EstadoItem.PocasUnidades);

            if (carrito.Items.Any(i => i.Estado is EstadoItem.Agotado or EstadoItem.NoDisponible or EstadoItem.StockInsuficiente))
                carrito.Avisos.Add("Algunos productos ya no están disponibles en la cantidad solicitada; ajústelos para continuar.");

            return carrito;
        }

        public async Task<ResultadoCarrito<CarritoDto>> Agregar(Guid usuarioId, AgregarItemRequest request)
        {
            string sku = Normalizar(request.sku);

            List<VarianteCatalogoDto>? variantes = await _catalogoCliente.ObtenerVariantes(new[] { sku });
            if (variantes == null)
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.ServicioNoDisponible, "No se pudo verificar el producto en este momento. Intente de nuevo.");

            VarianteCatalogoDto? variante = variantes.FirstOrDefault();
            if (variante == null)
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.NoEncontrado, "El producto no existe.");
            if (!variante.Disponible)
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.Conflicto, "El producto no está disponible para la venta.");

            int nuevaCantidad = await _carritoCapaDatos.ObtenerCantidad(usuarioId, sku) + request.cantidad;
            if (nuevaCantidad > MaxUnidadesPorProducto)
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.Conflicto, $"Máximo {MaxUnidadesPorProducto} unidades por producto.");

            string? errorStock = await ValidarStock(sku, nuevaCantidad);
            if (errorStock != null)
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.Conflicto, errorStock);

            int resultado = await _carritoCapaDatos.SumarCantidad(usuarioId, sku, request.cantidad, MaxUnidadesPorProducto, MaxProductos, Expiracion);
            if (resultado == ResultadoSuma.LimiteProductos)
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.Conflicto, $"El carrito admite como máximo {MaxProductos} productos distintos.");
            if (resultado == ResultadoSuma.LimiteUnidades)
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.Conflicto, $"Máximo {MaxUnidadesPorProducto} unidades por producto.");

            return ResultadoCarrito<CarritoDto>.Ok(await Obtener(usuarioId));
        }

        public async Task<ResultadoCarrito<CarritoDto>> ActualizarCantidad(Guid usuarioId, string sku, int cantidad)
        {
            string skuNormalizado = Normalizar(sku);
            if (await _carritoCapaDatos.ObtenerCantidad(usuarioId, skuNormalizado) == 0)
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.NoEncontrado, "El producto no está en el carrito.");

            string? errorStock = await ValidarStock(skuNormalizado, cantidad);
            if (errorStock != null)
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.Conflicto, errorStock);

            if (!await _carritoCapaDatos.EstablecerCantidad(usuarioId, skuNormalizado, cantidad, Expiracion))
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.NoEncontrado, "El producto no está en el carrito.");

            return ResultadoCarrito<CarritoDto>.Ok(await Obtener(usuarioId));
        }

        public async Task<ResultadoCarrito<CarritoDto>> Quitar(Guid usuarioId, string sku)
        {
            if (!await _carritoCapaDatos.QuitarItem(usuarioId, Normalizar(sku)))
                return ResultadoCarrito<CarritoDto>.Error(EstadoResultado.NoEncontrado, "El producto no está en el carrito.");

            return ResultadoCarrito<CarritoDto>.Ok(await Obtener(usuarioId));
        }

        public Task Vaciar(Guid usuarioId) => _carritoCapaDatos.VaciarCarrito(usuarioId);

        // Solo quita lo que se compró: si el cliente agregó algo nuevo mientras pagaba, eso se conserva.
        // Borrar en Redis es idempotente por naturaleza: si el evento llega dos veces, la segunda no hace nada.
        public Task<long> QuitarComprados(Guid usuarioId, IEnumerable<string> skus) =>
            _carritoCapaDatos.QuitarItems(usuarioId, skus.Select(Normalizar));

        // El stock es informativo: si Inventario no responde se permite continuar; la garantía real llega al reservar en el checkout.
        private async Task<string?> ValidarStock(string sku, int cantidad)
        {
            List<DisponibilidadInventarioDto>? disponibilidad = await _inventarioCliente.ObtenerDisponibilidad(new[] { sku });
            DisponibilidadInventarioDto? stock = disponibilidad?.FirstOrDefault();
            if (stock == null)
                return null;

            if (!stock.Existe || stock.Disponible <= 0)
                return "El producto está agotado.";
            if (cantidad > stock.Disponible)
                return $"Solo quedan {stock.Disponible} unidades disponibles.";
            return null;
        }

        private static ItemCarritoDto ConstruirItem(
            string sku, int cantidad,
            Dictionary<string, VarianteCatalogoDto>? variantes,
            Dictionary<string, DisponibilidadInventarioDto>? stock)
        {
            var item = new ItemCarritoDto { Sku = sku, Cantidad = cantidad };

            if (variantes == null)
                return item;

            if (!variantes.TryGetValue(sku, out VarianteCatalogoDto? variante) || !variante.Disponible)
            {
                item.Nombre = variante?.Nombre;
                item.Estado = EstadoItem.NoDisponible;
                return item;
            }

            item.Nombre = variante.Nombre;
            item.Slug = variante.Slug;
            item.Opciones = variante.Opciones;
            item.ImagenUrl = variante.ImagenUrl;
            item.PrecioUnitario = variante.Precio;
            item.PrecioAntes = variante.PrecioAntes;
            item.Subtotal = variante.Precio * cantidad;

            if (stock == null || !stock.TryGetValue(sku, out DisponibilidadInventarioDto? disponibilidad))
            {
                item.Estado = stock == null ? EstadoItem.Disponible : EstadoItem.Agotado;
                return item;
            }

            item.StockDisponible = disponibilidad.Disponible;
            item.Estado = disponibilidad.Disponible <= 0 ? EstadoItem.Agotado
                : cantidad > disponibilidad.Disponible ? EstadoItem.StockInsuficiente
                : disponibilidad.PocasUnidades ? EstadoItem.PocasUnidades
                : EstadoItem.Disponible;
            return item;
        }

        private static bool CuentaEnTotal(ItemCarritoDto item) =>
            item.Estado is EstadoItem.Disponible or EstadoItem.PocasUnidades or EstadoItem.StockInsuficiente;

        private static string Normalizar(string sku) => sku.Trim().ToUpperInvariant();
    }
}
