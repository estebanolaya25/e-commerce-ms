using CapaDatos.Excepciones;
using CapaDatos.Interfaz.categorias.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.cache.interfaz;
using CapaNegocios.Interfaz.categorias.interfaz;
using CapaNegocios.util;
using Microsoft.Extensions.Configuration;

namespace CapaNegocios.Implementacion.categorias.Implementacion
{
    public class clsCategoriasCapaNegocios : ICategoriasCapaNegocios
    {
        private const string MensajeNoEncontrada = "Categoría no encontrada.";

        private readonly ICategoriasCapaDatos _categoriasCapaDatos;
        private readonly ICacheCatalogo _cache;
        private readonly clsNotificadorCatalogo _notificador;
        private readonly IConfiguration _configuration;

        public clsCategoriasCapaNegocios(ICategoriasCapaDatos categoriasCapaDatos, ICacheCatalogo cache, clsNotificadorCatalogo notificador, IConfiguration configuration)
        {
            _categoriasCapaDatos = categoriasCapaDatos;
            _cache = cache;
            _notificador = notificador;
            _configuration = configuration;
        }

        private TimeSpan DuracionCache => TimeSpan.FromMinutes(int.TryParse(_configuration["Cache:MinutosDetalle"], out int valor) ? valor : 30);

        public async Task<List<CategoriaDto>> ObtenerArbol(bool incluirInactivas)
        {
            if (incluirInactivas)
                return ConstruirArbol(await _categoriasCapaDatos.ListarCategorias(true));

            List<CategoriaDto>? arbol = await _cache.ObtenerOCrear(
                "categorias:arbol",
                DuracionCache,
                async () => ConstruirArbol(await _categoriasCapaDatos.ListarCategorias(false)));

            return arbol ?? new List<CategoriaDto>();
        }

        public async Task<ResultadoCatalogo<CategoriaDto>> Crear(CategoriaRequest request)
        {
            string slug = clsSlug.Generar(string.IsNullOrWhiteSpace(request.slug) ? request.nombre : request.slug);
            if (slug.Length == 0)
                return ResultadoCatalogo<CategoriaDto>.Error(EstadoResultado.DatosInvalidos, "El nombre debe contener letras o números.");

            if (request.categoriaPadreId.HasValue && await _categoriasCapaDatos.ObtenerCategoriaPorId(request.categoriaPadreId.Value) == null)
                return ResultadoCatalogo<CategoriaDto>.Error(EstadoResultado.DatosInvalidos, "La categoría padre no existe.");

            try
            {
                Guid categoriaId = await _categoriasCapaDatos.CrearCategoria(request, slug);
                await _cache.Invalidar();
                return ResultadoCatalogo<CategoriaDto>.Ok((await _categoriasCapaDatos.ObtenerCategoriaPorId(categoriaId))!);
            }
            catch (ConflictoDatosException ex)
            {
                return ResultadoCatalogo<CategoriaDto>.Error(EstadoResultado.Conflicto, clsValidacionCatalogo.MensajeConflicto(ex));
            }
        }

        public async Task<ResultadoCatalogo<CategoriaDto>> Actualizar(Guid categoriaId, CategoriaRequest request)
        {
            if (await _categoriasCapaDatos.ObtenerCategoriaPorId(categoriaId) == null)
                return ResultadoCatalogo<CategoriaDto>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrada);

            string slug = clsSlug.Generar(string.IsNullOrWhiteSpace(request.slug) ? request.nombre : request.slug);
            if (slug.Length == 0)
                return ResultadoCatalogo<CategoriaDto>.Error(EstadoResultado.DatosInvalidos, "El nombre debe contener letras o números.");

            if (request.categoriaPadreId.HasValue)
            {
                Guid padreId = request.categoriaPadreId.Value;

                if (padreId == categoriaId)
                    return ResultadoCatalogo<CategoriaDto>.Error(EstadoResultado.DatosInvalidos, "Una categoría no puede ser su propia categoría padre.");

                if (await _categoriasCapaDatos.ObtenerCategoriaPorId(padreId) == null)
                    return ResultadoCatalogo<CategoriaDto>.Error(EstadoResultado.DatosInvalidos, "La categoría padre no existe.");

                if (await _categoriasCapaDatos.EsDescendiente(categoriaId, padreId))
                    return ResultadoCatalogo<CategoriaDto>.Error(EstadoResultado.DatosInvalidos, "La categoría padre no puede ser una subcategoría de esta misma categoría.");
            }

            try
            {
                await _categoriasCapaDatos.ActualizarCategoria(categoriaId, request, slug);
                await _cache.Invalidar();
                await _notificador.CatalogoModificado("Categoría actualizada");
                return ResultadoCatalogo<CategoriaDto>.Ok((await _categoriasCapaDatos.ObtenerCategoriaPorId(categoriaId))!);
            }
            catch (ConflictoDatosException ex)
            {
                return ResultadoCatalogo<CategoriaDto>.Error(EstadoResultado.Conflicto, clsValidacionCatalogo.MensajeConflicto(ex));
            }
        }

        public async Task<ResultadoCatalogo<bool>> CambiarEstado(Guid categoriaId, bool activa)
        {
            if (!await _categoriasCapaDatos.CambiarEstadoCategoria(categoriaId, activa))
                return ResultadoCatalogo<bool>.Error(EstadoResultado.NoEncontrado, MensajeNoEncontrada);

            await _cache.Invalidar();
            await _notificador.CatalogoModificado("Categoría activada o desactivada");
            return ResultadoCatalogo<bool>.Ok(true);
        }

        private static List<CategoriaDto> ConstruirArbol(List<CategoriaDto> categorias)
        {
            Dictionary<Guid, CategoriaDto> porId = categorias.ToDictionary(c => c.Id);
            var raices = new List<CategoriaDto>();

            foreach (CategoriaDto categoria in categorias)
            {
                if (categoria.CategoriaPadreId == null)
                    raices.Add(categoria);
                else if (porId.TryGetValue(categoria.CategoriaPadreId.Value, out CategoriaDto? padre))
                    padre.Hijas.Add(categoria);
            }

            return raices;
        }
    }
}
