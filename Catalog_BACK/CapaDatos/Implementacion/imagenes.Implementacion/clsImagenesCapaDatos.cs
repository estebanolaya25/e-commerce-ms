using CapaDatos.Interfaz.imagenes.interfaz;
using CapaDatos.util;
using CapaDto.Respuestas;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using System.Data.Common;

namespace CapaDatos.Implementacion.imagenes.Implementacion
{
    public class clsImagenesCapaDatos : IImagenesCapaDatos
    {
        private const string ColumnasImagen = "id, producto_id, variante_id, ruta_archivo, texto_alternativo, orden, es_principal";

        private readonly IConfiguration _configuration;

        public clsImagenesCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private NpgsqlConnection NuevaConexion() => new NpgsqlConnection(_configuration.GetConnectionString("PostgresConnectionString"));

        public async Task<ImagenDto?> ObtenerImagen(Guid imagenId)
        {
            const string query = "SELECT " + ColumnasImagen + " FROM catalogo.producto_imagenes WHERE id = @id";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, imagenId);
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapearImagen(reader) : null;
        }

        public Task<ImagenDto> CrearImagen(Guid productoId, Guid? varianteId, string rutaArchivo, string? textoAlternativo, bool esPrincipal)
        {
            const string queryEstado = @"
                SELECT COALESCE(MAX(orden) + 1, 0) AS siguiente_orden,
                       COALESCE(BOOL_OR(es_principal), false) AS tiene_principal
                FROM catalogo.producto_imagenes
                WHERE producto_id = @producto_id";

            const string queryInsertar = @"
                INSERT INTO catalogo.producto_imagenes (producto_id, variante_id, ruta_archivo, texto_alternativo, orden, es_principal)
                VALUES (@producto_id, @variante_id, @ruta_archivo, @texto_alternativo, @orden, @es_principal)
                RETURNING " + ColumnasImagen;

            return clsPostgresUtil.TraducirConflictos(async () =>
            {
                await using var connection = NuevaConexion();
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();

                await BloquearProducto(connection, transaction, productoId);

                int siguienteOrden;
                bool tienePrincipal;
                await using (var cmd = new NpgsqlCommand(queryEstado, connection, transaction))
                {
                    clsPostgresUtil.Agregar(cmd, "producto_id", NpgsqlDbType.Uuid, productoId);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    await reader.ReadAsync();
                    siguienteOrden = reader.GetInt32(reader.GetOrdinal("siguiente_orden"));
                    tienePrincipal = reader.GetBoolean(reader.GetOrdinal("tiene_principal"));
                }

                bool quedaPrincipal = esPrincipal || !tienePrincipal;
                if (quedaPrincipal && tienePrincipal)
                    await QuitarPrincipal(connection, transaction, productoId);

                ImagenDto imagen;
                await using (var cmd = new NpgsqlCommand(queryInsertar, connection, transaction))
                {
                    clsPostgresUtil.Agregar(cmd, "producto_id", NpgsqlDbType.Uuid, productoId);
                    clsPostgresUtil.Agregar(cmd, "variante_id", NpgsqlDbType.Uuid, varianteId);
                    clsPostgresUtil.Agregar(cmd, "ruta_archivo", NpgsqlDbType.Varchar, rutaArchivo);
                    clsPostgresUtil.Agregar(cmd, "texto_alternativo", NpgsqlDbType.Varchar, textoAlternativo);
                    clsPostgresUtil.Agregar(cmd, "orden", NpgsqlDbType.Integer, siguienteOrden);
                    clsPostgresUtil.Agregar(cmd, "es_principal", NpgsqlDbType.Boolean, quedaPrincipal);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    await reader.ReadAsync();
                    imagen = MapearImagen(reader);
                }

                await transaction.CommitAsync();
                return imagen;
            });
        }

        public async Task<string?> EliminarImagen(Guid imagenId)
        {
            const string queryEliminar = @"
                DELETE FROM catalogo.producto_imagenes
                WHERE id = @id
                RETURNING producto_id, ruta_archivo, es_principal";

            const string queryNuevaPrincipal = @"
                UPDATE catalogo.producto_imagenes
                SET es_principal = true
                WHERE id = (
                    SELECT id FROM catalogo.producto_imagenes
                    WHERE producto_id = @producto_id
                    ORDER BY orden, id
                    LIMIT 1
                )";

            ImagenDto? imagen = await ObtenerImagen(imagenId);
            if (imagen == null)
                return null;

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            await BloquearProducto(connection, transaction, imagen.ProductoId);

            string rutaArchivo;
            bool eraPrincipal;
            await using (var cmd = new NpgsqlCommand(queryEliminar, connection, transaction))
            {
                clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, imagenId);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return null;
                rutaArchivo = reader.GetString(reader.GetOrdinal("ruta_archivo"));
                eraPrincipal = reader.GetBoolean(reader.GetOrdinal("es_principal"));
            }

            if (eraPrincipal)
            {
                await using var cmd = new NpgsqlCommand(queryNuevaPrincipal, connection, transaction);
                clsPostgresUtil.Agregar(cmd, "producto_id", NpgsqlDbType.Uuid, imagen.ProductoId);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return rutaArchivo;
        }

        public async Task<bool> MarcarPrincipal(Guid imagenId)
        {
            const string query = "UPDATE catalogo.producto_imagenes SET es_principal = true WHERE id = @id";

            ImagenDto? imagen = await ObtenerImagen(imagenId);
            if (imagen == null)
                return false;

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            await BloquearProducto(connection, transaction, imagen.ProductoId);
            await QuitarPrincipal(connection, transaction, imagen.ProductoId);

            bool actualizada;
            await using (var cmd = new NpgsqlCommand(query, connection, transaction))
            {
                clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, imagenId);
                actualizada = await cmd.ExecuteNonQueryAsync() > 0;
            }

            await transaction.CommitAsync();
            return actualizada;
        }

        private static async Task BloquearProducto(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid productoId)
        {
            const string query = "SELECT 1 FROM catalogo.productos WHERE id = @producto_id FOR UPDATE";

            await using var cmd = new NpgsqlCommand(query, connection, transaction);
            clsPostgresUtil.Agregar(cmd, "producto_id", NpgsqlDbType.Uuid, productoId);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task QuitarPrincipal(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid productoId)
        {
            const string query = @"
                UPDATE catalogo.producto_imagenes
                SET es_principal = false
                WHERE producto_id = @producto_id AND es_principal";

            await using var cmd = new NpgsqlCommand(query, connection, transaction);
            clsPostgresUtil.Agregar(cmd, "producto_id", NpgsqlDbType.Uuid, productoId);
            await cmd.ExecuteNonQueryAsync();
        }

        private static ImagenDto MapearImagen(DbDataReader reader)
        {
            return new ImagenDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                ProductoId = reader.GetGuid(reader.GetOrdinal("producto_id")),
                VarianteId = clsPostgresUtil.LeerGuid(reader, "variante_id"),
                RutaArchivo = reader.GetString(reader.GetOrdinal("ruta_archivo")),
                TextoAlternativo = clsPostgresUtil.LeerTexto(reader, "texto_alternativo"),
                Orden = reader.GetInt32(reader.GetOrdinal("orden")),
                EsPrincipal = reader.GetBoolean(reader.GetOrdinal("es_principal"))
            };
        }
    }
}
