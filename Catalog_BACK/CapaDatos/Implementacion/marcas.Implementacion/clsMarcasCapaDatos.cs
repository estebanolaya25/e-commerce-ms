using CapaDatos.Interfaz.marcas.interfaz;
using CapaDatos.util;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using System.Data.Common;

namespace CapaDatos.Implementacion.marcas.Implementacion
{
    public class clsMarcasCapaDatos : IMarcasCapaDatos
    {
        private readonly IConfiguration _configuration;

        public clsMarcasCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private NpgsqlConnection NuevaConexion() => new NpgsqlConnection(_configuration.GetConnectionString("PostgresConnectionString"));

        public async Task<List<MarcaDto>> ListarMarcas(bool incluirInactivas)
        {
            const string query = @"
                SELECT id, nombre, slug, activa
                FROM catalogo.marcas
                WHERE @incluir_inactivas OR activa
                ORDER BY nombre";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "incluir_inactivas", NpgsqlDbType.Boolean, incluirInactivas);

            var marcas = new List<MarcaDto>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                marcas.Add(MapearMarca(reader));
            return marcas;
        }

        public async Task<MarcaDto?> ObtenerMarcaPorId(Guid marcaId)
        {
            const string query = "SELECT id, nombre, slug, activa FROM catalogo.marcas WHERE id = @id";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, marcaId);
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapearMarca(reader) : null;
        }

        public Task<Guid> CrearMarca(MarcaRequest request, string slug)
        {
            const string query = @"
                INSERT INTO catalogo.marcas (nombre, slug)
                VALUES (@nombre, @slug)
                RETURNING id";

            return clsPostgresUtil.TraducirConflictos(async () =>
            {
                await using var connection = NuevaConexion();
                await connection.OpenAsync();
                await using var cmd = new NpgsqlCommand(query, connection);
                clsPostgresUtil.Agregar(cmd, "nombre", NpgsqlDbType.Varchar, request.nombre.Trim());
                clsPostgresUtil.Agregar(cmd, "slug", NpgsqlDbType.Varchar, slug);
                return (Guid)(await cmd.ExecuteScalarAsync())!;
            });
        }

        public Task<bool> ActualizarMarca(Guid marcaId, MarcaRequest request, string slug)
        {
            const string query = "UPDATE catalogo.marcas SET nombre = @nombre, slug = @slug WHERE id = @id";

            return clsPostgresUtil.TraducirConflictos(async () =>
            {
                await using var connection = NuevaConexion();
                await connection.OpenAsync();
                await using var cmd = new NpgsqlCommand(query, connection);
                clsPostgresUtil.Agregar(cmd, "nombre", NpgsqlDbType.Varchar, request.nombre.Trim());
                clsPostgresUtil.Agregar(cmd, "slug", NpgsqlDbType.Varchar, slug);
                clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, marcaId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            });
        }

        public async Task<bool> CambiarEstadoMarca(Guid marcaId, bool activa)
        {
            const string query = "UPDATE catalogo.marcas SET activa = @activa WHERE id = @id";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "activa", NpgsqlDbType.Boolean, activa);
            clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, marcaId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static MarcaDto MapearMarca(DbDataReader reader)
        {
            return new MarcaDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                Slug = reader.GetString(reader.GetOrdinal("slug")),
                Activa = reader.GetBoolean(reader.GetOrdinal("activa"))
            };
        }
    }
}
