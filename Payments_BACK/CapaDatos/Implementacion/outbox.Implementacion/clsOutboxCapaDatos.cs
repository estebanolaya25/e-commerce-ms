using CapaDatos.Interfaz.outbox.interfaz;
using CapaDto.Modelos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace CapaDatos.Implementacion.outbox.Implementacion
{
    public class clsOutboxCapaDatos : IOutboxCapaDatos
    {
        private readonly IConfiguration _configuration;

        public clsOutboxCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection NuevaConexion() => new SqlConnection(_configuration.GetConnectionString("SQLConnectionString"));

        public async Task<int> ProcesarPendientes(int limite, Func<MensajeOutbox, Task> publicar)
        {
            const string querySeleccion = @"
                SELECT TOP (@Limite) OutboxId, Tipo, Contenido
                FROM pag.Outbox WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE PublicadoEn IS NULL
                ORDER BY CreadoEn";

            const string queryPublicado = "UPDATE pag.Outbox SET PublicadoEn = SYSUTCDATETIME(), Intentos = Intentos + 1 WHERE OutboxId = @OutboxId";
            const string queryError = "UPDATE pag.Outbox SET Intentos = Intentos + 1, UltimoError = @Error WHERE OutboxId = @OutboxId";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            var pendientes = new List<MensajeOutbox>();
            using (var cmd = new SqlCommand(querySeleccion, connection, transaction))
            {
                cmd.Parameters.Add("@Limite", SqlDbType.Int).Value = limite;
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    pendientes.Add(new MensajeOutbox { OutboxId = reader.GetGuid(0), Tipo = reader.GetString(1), Contenido = reader.GetString(2) });
            }

            int publicados = 0;
            foreach (MensajeOutbox mensaje in pendientes)
            {
                try
                {
                    await publicar(mensaje);
                    using var cmd = new SqlCommand(queryPublicado, connection, transaction);
                    cmd.Parameters.Add("@OutboxId", SqlDbType.UniqueIdentifier).Value = mensaje.OutboxId;
                    await cmd.ExecuteNonQueryAsync();
                    publicados++;
                }
                catch (Exception ex)
                {
                    using var cmd = new SqlCommand(queryError, connection, transaction);
                    cmd.Parameters.Add("@OutboxId", SqlDbType.UniqueIdentifier).Value = mensaje.OutboxId;
                    cmd.Parameters.Add("@Error", SqlDbType.NVarChar, 500).Value = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    await cmd.ExecuteNonQueryAsync();
                    break;
                }
            }

            await transaction.CommitAsync();
            return publicados;
        }
    }
}
