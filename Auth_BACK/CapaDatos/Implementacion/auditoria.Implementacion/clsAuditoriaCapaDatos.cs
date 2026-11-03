using CapaDatos.Interfaz.auditoria.interfaz;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace CapaDatos.Implementacion.auditoria.Implementacion
{
    public class clsAuditoriaCapaDatos : IAuditoriaCapaDatos
    {
        private readonly IConfiguration _configuration;

        public clsAuditoriaCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection NuevaConexion() => new SqlConnection(_configuration.GetConnectionString("SQLConnectionString"));

        public async Task RegistrarEvento(Guid? userId, string tipoEvento, bool exitoso, string? ip, string? userAgent, string? detalle)
        {
            const string query = @"
                INSERT INTO dbo.AuthAuditLogs (UserId, EventType, IsSuccessful, IpAddress, UserAgent, Details)
                VALUES (@UserId, @EventType, @IsSuccessful, @IpAddress, @UserAgent, @Details)";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = (object?)userId ?? DBNull.Value;
            cmd.Parameters.Add("@EventType", SqlDbType.NVarChar, 50).Value = tipoEvento;
            cmd.Parameters.Add("@IsSuccessful", SqlDbType.Bit).Value = exitoso;
            cmd.Parameters.Add("@IpAddress", SqlDbType.NVarChar, 45).Value = (object?)Recortar(ip, 45) ?? DBNull.Value;
            cmd.Parameters.Add("@UserAgent", SqlDbType.NVarChar, 500).Value = (object?)Recortar(userAgent, 500) ?? DBNull.Value;
            cmd.Parameters.Add("@Details", SqlDbType.NVarChar, 1000).Value = (object?)Recortar(detalle, 1000) ?? DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }

        private static string? Recortar(string? valor, int maximo)
        {
            if (string.IsNullOrEmpty(valor))
                return null;

            return valor.Length <= maximo ? valor : valor.Substring(0, maximo);
        }
    }
}
