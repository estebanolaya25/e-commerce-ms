using CapaDatos.Interfaz.categorias.interfaz;
using CapaDatos.util;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using System.Data.Common;

namespace CapaDatos.Implementacion.categorias.Implementacion
{
    public class clsCategoriasCapaDatos : ICategoriasCapaDatos
    {
        private readonly IConfiguration _configuration;

        public clsCategoriasCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private NpgsqlConnection NuevaConexion() => new NpgsqlConnection(_configuration.GetConnectionString("PostgresConnectionString"));

        public async Task<List<CategoriaDto>> ListarCategorias(bool incluirInactivas)
        {
            const string query = @"
                SELECT id, categoria_padre_id, nombre, slug, descripcion, orden, activa
                FROM catalogo.categorias
                WHERE @incluir_inactivas OR activa
                ORDER BY orden, nombre";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "incluir_inactivas", NpgsqlDbType.Boolean, incluirInactivas);

            var categorias = new List<CategoriaDto>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                categorias.Add(MapearCategoria(reader));
            return categorias;
        }

        public async Task<CategoriaDto?> ObtenerCategoriaPorId(Guid categoriaId)
        {
            const string query = @"
                SELECT id, categoria_padre_id, nombre, slug, descripcion, orden, activa
                FROM catalogo.categorias
                WHERE id = @id";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, categoriaId);
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapearCategoria(reader) : null;
        }

        public Task<Guid> CrearCategoria(CategoriaRequest request, string slug)
        {
            const string query = @"
                INSERT INTO catalogo.categorias (categoria_padre_id, nombre, slug, descripcion, orden)
                VALUES (@padre_id, @nombre, @slug, @descripcion, @orden)
                RETURNING id";

            return clsPostgresUtil.TraducirConflictos(async () =>
            {
                await using var connection = NuevaConexion();
                await connection.OpenAsync();
                await using var cmd = new NpgsqlCommand(query, connection);
                AgregarParametrosCategoria(cmd, request, slug);
                return (Guid)(await cmd.ExecuteScalarAsync())!;
            });
        }

        public Task<bool> ActualizarCategoria(Guid categoriaId, CategoriaRequest request, string slug)
        {
            const string query = @"
                UPDATE catalogo.categorias
                SET categoria_padre_id = @padre_id, nombre = @nombre, slug = @slug,
                    descripcion = @descripcion, orden = @orden
                WHERE id = @id";

            return clsPostgresUtil.TraducirConflictos(async () =>
            {
                await using var connection = NuevaConexion();
                await connection.OpenAsync();
                await using var cmd = new NpgsqlCommand(query, connection);
                AgregarParametrosCategoria(cmd, request, slug);
                clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, categoriaId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            });
        }

        public async Task<bool> CambiarEstadoCategoria(Guid categoriaId, bool activa)
        {
            const string query = "UPDATE catalogo.categorias SET activa = @activa WHERE id = @id";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "activa", NpgsqlDbType.Boolean, activa);
            clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, categoriaId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> EsDescendiente(Guid categoriaId, Guid posibleDescendienteId)
        {
            const string query = @"
                WITH RECURSIVE descendientes AS (
                    SELECT id FROM catalogo.categorias WHERE categoria_padre_id = @id
                    UNION
                    SELECT c.id
                    FROM catalogo.categorias c
                    INNER JOIN descendientes d ON c.categoria_padre_id = d.id
                )
                SELECT EXISTS (SELECT 1 FROM descendientes WHERE id = @posible)";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, categoriaId);
            clsPostgresUtil.Agregar(cmd, "posible", NpgsqlDbType.Uuid, posibleDescendienteId);
            return (bool)(await cmd.ExecuteScalarAsync())!;
        }

        private static void AgregarParametrosCategoria(NpgsqlCommand cmd, CategoriaRequest request, string slug)
        {
            clsPostgresUtil.Agregar(cmd, "padre_id", NpgsqlDbType.Uuid, request.categoriaPadreId);
            clsPostgresUtil.Agregar(cmd, "nombre", NpgsqlDbType.Varchar, request.nombre.Trim());
            clsPostgresUtil.Agregar(cmd, "slug", NpgsqlDbType.Varchar, slug);
            clsPostgresUtil.Agregar(cmd, "descripcion", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(request.descripcion) ? null : request.descripcion.Trim());
            clsPostgresUtil.Agregar(cmd, "orden", NpgsqlDbType.Integer, request.orden);
        }

        private static CategoriaDto MapearCategoria(DbDataReader reader)
        {
            return new CategoriaDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                CategoriaPadreId = clsPostgresUtil.LeerGuid(reader, "categoria_padre_id"),
                Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                Slug = reader.GetString(reader.GetOrdinal("slug")),
                Descripcion = clsPostgresUtil.LeerTexto(reader, "descripcion"),
                Orden = reader.GetInt32(reader.GetOrdinal("orden")),
                Activa = reader.GetBoolean(reader.GetOrdinal("activa"))
            };
        }
    }
}
