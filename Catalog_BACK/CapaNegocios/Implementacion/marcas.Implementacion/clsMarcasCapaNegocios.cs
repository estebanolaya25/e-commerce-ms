using CapaDatos.Excepciones;
using CapaDatos.Interfaz.marcas.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.cache.interfaz;
using CapaNegocios.Interfaz.marcas.interfaz;
using CapaNegocios.util;
using Microsoft.Extensions.Configuration;

namespace CapaNegocios.Implementacion.marcas.Implementacion
{
    public class clsMarcasCapaNegocios : IMarcasCapaNegocios
    {
        private const string MensajeNoEncontrada = "Marca no encontrada.";

        private readonly IMarcasCapaDatos _marcasCapaDatos;
        private readonly ICacheCatalogo _cache;
        private readonly clsNotificadorCatalogo _notificador;
        private readonly IConfiguration _configuration;

        public clsMarcasCapaNegocios(IMarcasCapaDatos marcasCapaDatos, ICacheCatalogo cache, clsNotificadorCatalogo notificador, IConfiguration configuration)
        {
            _marcasCapaDatos = marcasCapaDatos;
            _cache = cache;
            _notificador = notificador;
            _configuration = configuration;
        }

        private TimeSpan DuracionCache => TimeSpan.FromMinutes(int.TryParse(_configuration["Cache:MinutosDetalle"], out int valor) ? valor : 30);

        public async Task<List<MarcaDto>> Listar(bool incluirInactivas)
        {
            if (incluirInactivas)
                return await _marcasCapaDatos.ListarMarcas(true);

            List<MarcaDto>? marcas = await _cache.ObtenerOCrear(
                "marcas:listado",
                DuracionCache,
                async () => await _marcasCapaDatos.ListarMarcas(false));

            return marcas ?? new List<MarcaDto>();
        }

        public async Task<ResultadoCatalogo<MarcaDto>> Crear(MarcaRequest request)
        {
            string slug = clsSlug.Generar(string.IsNullOrWhiteSpace(request.slug) ? request.nombre : request.slug);
            if (slug.Length == 0)
                return ResultadoCatalogo<MarcaDto>.Error(EstadoResultado.DatosInvalidos, "El nombre debe contener letras o números.");

            try
            {
                Guid marcaId = await _marcasCapaDatos.CrearMarca(request, slug);
                await _cache.Invalidar();
                return ResultadoCatalogo<MarcaDto>.Ok((await _marcasCapaDatos.ObtenerMarcaPorId(marcaId))!);
            }
            catch (ConflictoDatosException ex)
            {
                return ResultadoCatalogo<MarcaDto>.Error(EstadoResultado.Conflicto, clsValidacionCatalogo.MensajeConflicto(ex));
            }
        }

        public async Task<ResultadoCatalogo<MarcaDto>> Actualizar(Guid marcaId, MarcaRequest request)
        {
            string slug = clsSlug.Generar(string.IsNullOrWhiteSpace(request.slug) ? request.nombre : request.slug);
            if (slug.Length == 0)
                return ResultadoCatalogo<MarcaDto>.Error(EstadoResultado.DatosInvalidos, "El nombre debe contener letras o números.");

            try
            {
                if (!await _marcasCapaDatos.ActualizarMarca(marcaId, request, slug))
                    return ResultadoCatalogo<MarcaDto>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrada);

                await _cache.Invalidar();
                await _notificador.CatalogoModificado("Marca actualizada");
                return ResultadoCatalogo<MarcaDto>.Ok((await _marcasCapaDatos.ObtenerMarcaPorId(marcaId))!);
            }
            catch (ConflictoDatosException ex)
            {
                return ResultadoCatalogo<MarcaDto>.Error(EstadoResultado.Conflicto, clsValidacionCatalogo.MensajeConflicto(ex));
            }
        }

        public async Task<ResultadoCatalogo<bool>> CambiarEstado(Guid marcaId, bool activa)
        {
            if (!await _marcasCapaDatos.CambiarEstadoMarca(marcaId, activa))
                return ResultadoCatalogo<bool>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrada);

            await _cache.Invalidar();
            await _notificador.CatalogoModificado("Marca activada o desactivada");
            return ResultadoCatalogo<bool>.Ok(true);
        }
    }
}
