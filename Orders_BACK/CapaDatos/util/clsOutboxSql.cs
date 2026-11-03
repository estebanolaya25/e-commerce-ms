using CapaDto.Modelos;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CapaDatos.util
{
    public static class clsOutboxSql
    {
        public const int ErrorClaveDuplicada = 2627;
        public const int ErrorIndiceUnico = 2601;

        public static async Task Agregar(SqlConnection connection, SqlTransaction transaction, EventoOutbox evento)
        {
            const string query = "INSERT INTO ped.Outbox (OutboxId, Tipo, Contenido) VALUES (@OutboxId, @Tipo, @Contenido)";

            using var cmd = new SqlCommand(query, connection, transaction);
            cmd.Parameters.Add("@OutboxId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            cmd.Parameters.Add("@Tipo", SqlDbType.VarChar, 100).Value = evento.Tipo;
            cmd.Parameters.Add("@Contenido", SqlDbType.NVarChar, -1).Value = evento.Contenido;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
