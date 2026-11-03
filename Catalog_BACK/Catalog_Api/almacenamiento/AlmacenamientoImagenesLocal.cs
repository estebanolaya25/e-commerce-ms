using CapaNegocios.Interfaz.imagenes.interfaz;

namespace Catalog_Api.almacenamiento
{
    public class AlmacenamientoImagenesLocal : IAlmacenamientoImagenes
    {
        private readonly string _rutaRaiz;
        private readonly string _urlBase;

        public AlmacenamientoImagenesLocal(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _rutaRaiz = ObtenerRutaRaiz(configuration, environment);
            _urlBase = (configuration["Imagenes:UrlBase"] ?? "/api/catalogo/imagenes").TrimEnd('/');
            Directory.CreateDirectory(_rutaRaiz);
        }

        public static string ObtenerRutaRaiz(IConfiguration configuration, IWebHostEnvironment environment)
        {
            string ruta = configuration["Imagenes:RutaFisica"] ?? "imagenes";
            return Path.GetFullPath(Path.IsPathRooted(ruta) ? ruta : Path.Combine(environment.ContentRootPath, ruta));
        }

        public async Task<string> Guardar(byte[] contenido, string carpeta, string extension)
        {
            string rutaRelativa = $"{carpeta.Trim('/')}/{Guid.NewGuid():N}.{extension}";
            string rutaCompleta = ResolverRuta(rutaRelativa);

            Directory.CreateDirectory(Path.GetDirectoryName(rutaCompleta)!);
            await File.WriteAllBytesAsync(rutaCompleta, contenido);
            return rutaRelativa;
        }

        public Task Eliminar(string rutaArchivo)
        {
            string rutaCompleta = ResolverRuta(rutaArchivo);
            if (File.Exists(rutaCompleta))
                File.Delete(rutaCompleta);
            return Task.CompletedTask;
        }

        public string ObtenerUrl(string rutaArchivo) => $"{_urlBase}/{rutaArchivo}";

        private string ResolverRuta(string rutaRelativa)
        {
            string rutaCompleta = Path.GetFullPath(Path.Combine(_rutaRaiz, rutaRelativa));
            string raizConSeparador = _rutaRaiz.EndsWith(Path.DirectorySeparatorChar) ? _rutaRaiz : _rutaRaiz + Path.DirectorySeparatorChar;

            if (!rutaCompleta.StartsWith(raizConSeparador, StringComparison.Ordinal))
                throw new InvalidOperationException("Ruta de imagen fuera de la carpeta permitida.");

            return rutaCompleta;
        }
    }
}
