using CapaDatos.Excepciones;
using CapaDatos.Interfaz.categorias.interfaz;
using CapaDatos.Interfaz.marcas.interfaz;
using CapaDatos.Interfaz.productos.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.cache.interfaz;
using CapaNegocios.Interfaz.eventos.interfaz;
using CapaNegocios.Interfaz.imagenes.interfaz;
using CapaNegocios.Interfaz.productos.interfaz;
using CapaNegocios.util;
using Ecommerce.Contratos.Catalogo;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace CapaNegocios.Implementacion.productos.Implementacion
{
    public class clsProductosCapaNegocios : IProductosCapaNegocios
    {
        private const string MensajeProductoNoEncontrado = "Producto no encontrado.";
        private const string MensajeVarianteNoEncontrada = "Variante no encontrada.";

        private readonly IProductosCapaDatos _productosCapaDatos;
        private readonly ICategoriasCapaDatos _categoriasCapaDatos;
        private readonly IMarcasCapaDatos _marcasCapaDatos;
        private readonly IAlmacenamientoImagenes _almacenamientoImagenes;
        private readonly ICacheCatalogo _cache;
        private readonly IPublicadorEventos _publicadorEventos;
        private readonly clsNotificadorCatalogo _notificador;
        private readonly IConfiguration _configuration;

        public clsProductosCapaNegocios(
            IProductosCapaDatos productosCapaDatos,
            ICategoriasCapaDatos categoriasCapaDatos,
            IMarcasCapaDatos marcasCapaDatos,
            IAlmacenamientoImagenes almacenamientoImagenes,
            ICacheCatalogo cache,
            IPublicadorEventos publicadorEventos,
            clsNotificadorCatalogo notificador,
            IConfiguration configuration)
        {
            _productosCapaDatos = productosCapaDatos;
            _categoriasCapaDatos = categoriasCapaDatos;
            _marcasCapaDatos = marcasCapaDatos;
            _almacenamientoImagenes = almacenamientoImagenes;
            _cache = cache;
            _publicadorEventos = publicadorEventos;
            _notificador = notificador;
            _configuration = configuration;
        }

        private TimeSpan DuracionListado => TimeSpan.FromMinutes(int.TryParse(_configuration["Cache:MinutosListado"], out int valor) ? valor : 5);
        private TimeSpan DuracionDetalle => TimeSpan.FromMinutes(int.TryParse(_configuration["Cache:MinutosDetalle"], out int valor) ? valor : 30);

        public async Task<PaginaDto<ProductoResumenDto>> Listar(FiltroProductosRequest filtro, bool incluirInactivos)
        {
            filtro.categoria = NormalizarFiltro(filtro.categoria);
            filtro.marca = NormalizarFiltro(filtro.marca);
            filtro.buscar = string.IsNullOrWhiteSpace(filtro.buscar) ? null : filtro.buscar.Trim();

            if (incluirInactivos)
                return await ConsultarListado(filtro, true);

            PaginaDto<ProductoResumenDto>? pagina = await _cache.ObtenerOCrear(
                ClaveListado(filtro),
                DuracionListado,
                async () => await ConsultarListado(filtro, false));

            return pagina ?? new PaginaDto<ProductoResumenDto> { Pagina = filtro.pagina, Tamano = filtro.tamano };
        }

        public Task<ProductoDetalleDto?> ObtenerPorSlug(string slug)
        {
            string slugNormalizado = slug.Trim().ToLowerInvariant();

            return _cache.ObtenerOCrear(
                $"productos:detalle:{slugNormalizado}",
                DuracionDetalle,
                async () =>
                {
                    ProductoDetalleDto? producto = await _productosCapaDatos.ObtenerProductoPorSlug(slugNormalizado, false);
                    if (producto == null || producto.Variantes.Count == 0)
                        return null;
                    return CompletarUrls(producto);
                });
        }

        public async Task<ProductoDetalleDto?> ObtenerPorId(Guid productoId)
        {
            ProductoDetalleDto? producto = await _productosCapaDatos.ObtenerProductoPorId(productoId, true);
            return producto == null ? null : CompletarUrls(producto);
        }

        public async Task<ResultadoCatalogo<ProductoDetalleDto>> Crear(CrearProductoRequest request, Guid? usuarioId)
        {
            string? error = await ValidarProducto(request)
                ?? clsValidacionCatalogo.ValidarDiccionario(request.atributos, "atributos");
            if (error != null)
                return ResultadoCatalogo<ProductoDetalleDto>.Error(EstadoResultado.DatosInvalidos, error);

            foreach (VarianteRequest variante in request.variantes)
            {
                error = ValidarVariante(variante, request.precioBase);
                if (error != null)
                    return ResultadoCatalogo<ProductoDetalleDto>.Error(EstadoResultado.DatosInvalidos, error);
            }

            string slug = clsSlug.Generar(string.IsNullOrWhiteSpace(request.slug) ? request.nombre : request.slug);
            if (slug.Length == 0)
                return ResultadoCatalogo<ProductoDetalleDto>.Error(EstadoResultado.DatosInvalidos, "El nombre debe contener letras o números.");

            try
            {
                Guid productoId = await _productosCapaDatos.CrearProducto(request, slug, usuarioId);
                await _cache.Invalidar();

                ProductoDetalleDto creado = (await ObtenerPorId(productoId))!;
                foreach (VarianteDto variante in creado.Variantes)
                    await PublicarVarianteCreada(variante);
                await _notificador.ProductoModificado(productoId);

                return ResultadoCatalogo<ProductoDetalleDto>.Ok(creado);
            }
            catch (ConflictoDatosException ex)
            {
                return ResultadoCatalogo<ProductoDetalleDto>.Error(EstadoResultado.Conflicto, clsValidacionCatalogo.MensajeConflicto(ex));
            }
        }

        public async Task<ResultadoCatalogo<ProductoDetalleDto>> Actualizar(Guid productoId, ProductoRequest request, Guid? usuarioId)
        {
            ProductoDetalleDto? actual = await _productosCapaDatos.ObtenerProductoPorId(productoId, true);
            if (actual == null)
                return ResultadoCatalogo<ProductoDetalleDto>.Error(EstadoResultado.NoEncontrado, MensajeProductoNoEncontrado);

            string? error = await ValidarProducto(request)
                ?? clsValidacionCatalogo.ValidarDiccionario(request.atributos, "atributos");
            if (error != null)
                return ResultadoCatalogo<ProductoDetalleDto>.Error(EstadoResultado.DatosInvalidos, error);

            VarianteDto? afectada = actual.Variantes.FirstOrDefault(v =>
                v.PrecioPropio == null && v.PrecioAntes.HasValue && v.PrecioAntes.Value <= request.precioBase);
            if (afectada != null)
                return ResultadoCatalogo<ProductoDetalleDto>.Error(EstadoResultado.DatosInvalidos,
                    $"El nuevo precio base dejaría a la variante {afectada.Sku} con un precio anterior menor o igual al actual.");

            string slug = clsSlug.Generar(string.IsNullOrWhiteSpace(request.slug) ? request.nombre : request.slug);
            if (slug.Length == 0)
                return ResultadoCatalogo<ProductoDetalleDto>.Error(EstadoResultado.DatosInvalidos, "El nombre debe contener letras o números.");

            try
            {
                await _productosCapaDatos.ActualizarProducto(productoId, request, slug, usuarioId);
                await _cache.Invalidar();
                await _notificador.ProductoModificado(productoId);
                return ResultadoCatalogo<ProductoDetalleDto>.Ok((await ObtenerPorId(productoId))!);
            }
            catch (ConflictoDatosException ex)
            {
                return ResultadoCatalogo<ProductoDetalleDto>.Error(EstadoResultado.Conflicto, clsValidacionCatalogo.MensajeConflicto(ex));
            }
        }

        public async Task<ResultadoCatalogo<bool>> CambiarEstado(Guid productoId, bool activo, Guid? usuarioId)
        {
            if (!await _productosCapaDatos.CambiarEstadoProducto(productoId, activo, usuarioId))
                return ResultadoCatalogo<bool>.Error(EstadoResultado.NoEncontrado, MensajeProductoNoEncontrado);

            await _cache.Invalidar();
            await _notificador.ProductoModificado(productoId);
            return ResultadoCatalogo<bool>.Ok(true);
        }

        public async Task<ResultadoCatalogo<List<VarianteResumenDto>>> ObtenerVariantesPorSku(string? skus)
        {
            List<string> lista = (skus ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpperInvariant())
                .Distinct()
                .ToList();

            if (lista.Count == 0 || lista.Count > 50 || lista.Any(s => s.Length > 50))
                return ResultadoCatalogo<List<VarianteResumenDto>>.Error(EstadoResultado.DatosInvalidos, "Indique entre 1 y 50 SKU separados por comas.");

            List<VarianteResumenDto> variantes = await _productosCapaDatos.ObtenerVariantesPorSku(lista);
            foreach (VarianteResumenDto variante in variantes)
            {
                if (variante.ImagenRuta != null)
                    variante.ImagenUrl = _almacenamientoImagenes.ObtenerUrl(variante.ImagenRuta);
                if (variante.PrecioAntes <= variante.Precio)
                    variante.PrecioAntes = null;
            }
            return ResultadoCatalogo<List<VarianteResumenDto>>.Ok(variantes);
        }

        public async Task<ResultadoCatalogo<VarianteDto>> AgregarVariante(Guid productoId, VarianteRequest request)
        {
            ProductoDetalleDto? producto = await _productosCapaDatos.ObtenerProductoPorId(productoId, true);
            if (producto == null)
                return ResultadoCatalogo<VarianteDto>.Error(EstadoResultado.NoEncontrado, MensajeProductoNoEncontrado);

            string? error = ValidarVariante(request, producto.PrecioBase);
            if (error != null)
                return ResultadoCatalogo<VarianteDto>.Error(EstadoResultado.DatosInvalidos, error);

            try
            {
                Guid varianteId = await _productosCapaDatos.CrearVariante(productoId, request);
                await _cache.Invalidar();

                VarianteDto creada = (await _productosCapaDatos.ObtenerVariante(varianteId))!;
                await PublicarVarianteCreada(creada);
                await _notificador.ProductoModificado(productoId);
                return ResultadoCatalogo<VarianteDto>.Ok(creada);
            }
            catch (ConflictoDatosException ex)
            {
                return ResultadoCatalogo<VarianteDto>.Error(EstadoResultado.Conflicto, clsValidacionCatalogo.MensajeConflicto(ex));
            }
        }

        public async Task<ResultadoCatalogo<VarianteDto>> ActualizarVariante(Guid varianteId, VarianteRequest request)
        {
            VarianteDto? variante = await _productosCapaDatos.ObtenerVariante(varianteId);
            if (variante == null)
                return ResultadoCatalogo<VarianteDto>.Error(EstadoResultado.NoEncontrado, MensajeVarianteNoEncontrada);

            if (!string.Equals(request.sku.Trim(), variante.Sku, StringComparison.OrdinalIgnoreCase))
                return ResultadoCatalogo<VarianteDto>.Error(EstadoResultado.DatosInvalidos,
                    "El SKU no se puede cambiar: Inventario, Carrito y Pedidos lo usan como identificador. Cree una variante nueva.");

            ProductoDetalleDto producto = (await _productosCapaDatos.ObtenerProductoPorId(variante.ProductoId, true))!;
            string? error = ValidarVariante(request, producto.PrecioBase);
            if (error != null)
                return ResultadoCatalogo<VarianteDto>.Error(EstadoResultado.DatosInvalidos, error);

            try
            {
                await _productosCapaDatos.ActualizarVariante(varianteId, request);
                await _cache.Invalidar();
                await _notificador.ProductoModificado(variante.ProductoId);
                return ResultadoCatalogo<VarianteDto>.Ok((await _productosCapaDatos.ObtenerVariante(varianteId))!);
            }
            catch (ConflictoDatosException ex)
            {
                return ResultadoCatalogo<VarianteDto>.Error(EstadoResultado.Conflicto, clsValidacionCatalogo.MensajeConflicto(ex));
            }
        }

        public async Task<ResultadoCatalogo<bool>> CambiarEstadoVariante(Guid varianteId, bool activa)
        {
            if (!await _productosCapaDatos.CambiarEstadoVariante(varianteId, activa))
                return ResultadoCatalogo<bool>.Error(EstadoResultado.NoEncontrado, MensajeVarianteNoEncontrada);

            await _cache.Invalidar();
            VarianteDto? variante = await _productosCapaDatos.ObtenerVariante(varianteId);
            if (variante != null)
                await _notificador.ProductoModificado(variante.ProductoId);
            return ResultadoCatalogo<bool>.Ok(true);
        }

        private async Task<PaginaDto<ProductoResumenDto>> ConsultarListado(FiltroProductosRequest filtro, bool incluirInactivos)
        {
            var (items, total) = await _productosCapaDatos.ListarProductos(filtro, incluirInactivos);

            foreach (ProductoResumenDto item in items)
            {
                if (item.ImagenRuta != null)
                    item.ImagenUrl = _almacenamientoImagenes.ObtenerUrl(item.ImagenRuta);
                if (item.PrecioAntes <= item.PrecioDesde)
                    item.PrecioAntes = null;
            }

            return new PaginaDto<ProductoResumenDto>
            {
                Items = items,
                Pagina = filtro.pagina,
                Tamano = filtro.tamano,
                Total = total
            };
        }

        private Task PublicarVarianteCreada(VarianteDto variante)
        {
            return _publicadorEventos.Publicar(new VarianteCreada
            {
                Sku = variante.Sku,
                ProductoId = variante.ProductoId,
                VarianteId = variante.Id,
                CreadaEn = DateTime.UtcNow
            });
        }

        private async Task<string?> ValidarProducto(ProductoRequest request)
        {
            if (await _categoriasCapaDatos.ObtenerCategoriaPorId(request.categoriaId) == null)
                return "La categoría no existe.";

            if (request.marcaId.HasValue && await _marcasCapaDatos.ObtenerMarcaPorId(request.marcaId.Value) == null)
                return "La marca no existe.";

            return null;
        }

        private static string? ValidarVariante(VarianteRequest variante, decimal precioBase)
        {
            string? error = clsValidacionCatalogo.ValidarDiccionario(variante.opciones, "opciones");
            if (error != null)
                return error;

            decimal precioEfectivo = variante.precio ?? precioBase;
            if (variante.precioAntes.HasValue && variante.precioAntes.Value <= precioEfectivo)
                return $"En la variante {variante.sku}, el precio anterior debe ser mayor que el precio de venta.";

            return null;
        }

        private ProductoDetalleDto CompletarUrls(ProductoDetalleDto producto)
        {
            foreach (ImagenDto imagen in producto.Imagenes)
                imagen.Url = _almacenamientoImagenes.ObtenerUrl(imagen.RutaArchivo);
            return producto;
        }

        private static string? NormalizarFiltro(string? valor)
        {
            return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToLowerInvariant();
        }

        private static string ClaveListado(FiltroProductosRequest filtro)
        {
            return string.Join(':',
                "productos:listado",
                filtro.pagina,
                filtro.tamano,
                filtro.categoria ?? "-",
                filtro.marca ?? "-",
                filtro.buscar?.ToLowerInvariant() ?? "-",
                filtro.precioMin?.ToString(CultureInfo.InvariantCulture) ?? "-",
                filtro.precioMax?.ToString(CultureInfo.InvariantCulture) ?? "-",
                filtro.soloDestacados ? "1" : "0",
                filtro.orden);
        }
    }
}
