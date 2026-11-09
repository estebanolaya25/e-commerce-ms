using CapaDatos.Interfaz.imagenes.interfaz;
using CapaDatos.Interfaz.productos.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.cache.interfaz;
using CapaNegocios.Interfaz.imagenes.interfaz;
using CapaNegocios.util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CapaNegocios.Implementacion.imagenes.Implementacion
{
    public class clsImagenesCapaNegocios : IImagenesCapaNegocios
    {
        private const string MensajeImagenNoEncontrada = "Imagen no encontrada.";

        private readonly IImagenesCapaDatos _imagenesCapaDatos;
        private readonly IProductosCapaDatos _productosCapaDatos;
        private readonly IAlmacenamientoImagenes _almacenamientoImagenes;
        private readonly ICacheCatalogo _cache;
        private readonly clsNotificadorCatalogo _notificador;
        private readonly IConfiguration _configuration;
        private readonly ILogger<clsImagenesCapaNegocios> _logger;

        public clsImagenesCapaNegocios(
            IImagenesCapaDatos imagenesCapaDatos,
            IProductosCapaDatos productosCapaDatos,
            IAlmacenamientoImagenes almacenamientoImagenes,
            ICacheCatalogo cache,
            clsNotificadorCatalogo notificador,
            IConfiguration configuration,
            ILogger<clsImagenesCapaNegocios> logger)
        {
            _imagenesCapaDatos = imagenesCapaDatos;
            _productosCapaDatos = productosCapaDatos;
            _almacenamientoImagenes = almacenamientoImagenes;
            _cache = cache;
            _notificador = notificador;
            _configuration = configuration;
            _logger = logger;
        }

        private long TamanoMaximoBytes => (long.TryParse(_configuration["Imagenes:TamanoMaximoMB"], out long valor) ? valor : 5) * 1024 * 1024;
        private int MaximoPorProducto => int.TryParse(_configuration["Imagenes:MaximoPorProducto"], out int valor) ? valor : 10;

        public async Task<ResultadoCatalogo<ImagenDto>> Subir(Guid productoId, ImagenRequest request)
        {
            ProductoDetalleDto? producto = await _productosCapaDatos.ObtenerProductoPorId(productoId, true);
            if (producto == null)
                return ResultadoCatalogo<ImagenDto>.Error(EstadoResultado.NoEncontrado, "Producto no encontrado.");

            if (producto.Imagenes.Count >= MaximoPorProducto)
                return ResultadoCatalogo<ImagenDto>.Error(EstadoResultado.DatosInvalidos, $"El producto ya tiene el máximo de {MaximoPorProducto} imágenes.");

            if (request.VarianteId.HasValue && producto.Variantes.All(v => v.Id != request.VarianteId.Value))
                return ResultadoCatalogo<ImagenDto>.Error(EstadoResultado.DatosInvalidos, "La variante no pertenece a este producto.");

            if (request.TextoAlternativo?.Length > 200)
                return ResultadoCatalogo<ImagenDto>.Error(EstadoResultado.DatosInvalidos, "El texto alternativo admite como máximo 200 caracteres.");

            if (request.Tamano <= 0 || request.Tamano > TamanoMaximoBytes)
                return ResultadoCatalogo<ImagenDto>.Error(EstadoResultado.DatosInvalidos, $"La imagen debe pesar como máximo {TamanoMaximoBytes / 1024 / 1024} MB.");

            byte[]? contenido = await LeerContenido(request.Contenido);
            if (contenido == null)
                return ResultadoCatalogo<ImagenDto>.Error(EstadoResultado.DatosInvalidos, $"La imagen debe pesar como máximo {TamanoMaximoBytes / 1024 / 1024} MB.");

            string? extension = DetectarExtension(contenido);
            if (extension == null)
                return ResultadoCatalogo<ImagenDto>.Error(EstadoResultado.DatosInvalidos, "Formato no permitido. Solo se aceptan imágenes JPG, PNG o WEBP.");

            string rutaArchivo = await _almacenamientoImagenes.Guardar(contenido, $"productos/{productoId:N}", extension);

            try
            {
                string? texto = string.IsNullOrWhiteSpace(request.TextoAlternativo) ? null : request.TextoAlternativo.Trim();
                ImagenDto imagen = await _imagenesCapaDatos.CrearImagen(productoId, request.VarianteId, rutaArchivo, texto, request.EsPrincipal);
                imagen.Url = _almacenamientoImagenes.ObtenerUrl(imagen.RutaArchivo);
                await _cache.Invalidar();
                await _notificador.ProductoModificado(productoId);
                return ResultadoCatalogo<ImagenDto>.Ok(imagen);
            }
            catch
            {
                await _almacenamientoImagenes.Eliminar(rutaArchivo);
                throw;
            }
        }

        public async Task<ResultadoCatalogo<bool>> Eliminar(Guid imagenId)
        {
            // Se lee antes de borrarla para saber de qué producto era.
            ImagenDto? imagen = await _imagenesCapaDatos.ObtenerImagen(imagenId);
            string? rutaArchivo = await _imagenesCapaDatos.EliminarImagen(imagenId);
            if (rutaArchivo == null)
                return ResultadoCatalogo<bool>.Error(EstadoResultado.NoEncontrado, MensajeImagenNoEncontrada);

            await _cache.Invalidar();
            if (imagen != null)
                await _notificador.ProductoModificado(imagen.ProductoId);

            try
            {
                await _almacenamientoImagenes.Eliminar(rutaArchivo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "La imagen {Ruta} se eliminó de la base de datos pero no del disco.", rutaArchivo);
            }

            return ResultadoCatalogo<bool>.Ok(true);
        }

        public async Task<ResultadoCatalogo<bool>> MarcarPrincipal(Guid imagenId)
        {
            if (!await _imagenesCapaDatos.MarcarPrincipal(imagenId))
                return ResultadoCatalogo<bool>.Error(EstadoResultado.NoEncontrado, MensajeImagenNoEncontrada);

            await _cache.Invalidar();
            ImagenDto? principal = await _imagenesCapaDatos.ObtenerImagen(imagenId);
            if (principal != null)
                await _notificador.ProductoModificado(principal.ProductoId);
            return ResultadoCatalogo<bool>.Ok(true);
        }

        private async Task<byte[]?> LeerContenido(Stream origen)
        {
            using var memoria = new MemoryStream();
            byte[] buffer = new byte[81920];
            int leidos;

            while ((leidos = await origen.ReadAsync(buffer)) > 0)
            {
                if (memoria.Length + leidos > TamanoMaximoBytes)
                    return null;
                memoria.Write(buffer, 0, leidos);
            }

            return memoria.ToArray();
        }

        private static string? DetectarExtension(byte[] contenido)
        {
            if (contenido.Length >= 3 && contenido[0] == 0xFF && contenido[1] == 0xD8 && contenido[2] == 0xFF)
                return "jpg";

            if (contenido.Length >= 8 && contenido.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
                return "png";

            if (contenido.Length >= 12
                && contenido.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && contenido.AsSpan(8, 4).SequenceEqual("WEBP"u8))
                return "webp";

            return null;
        }
    }
}
