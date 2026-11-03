using CapaDatos.Excepciones;
using Npgsql;
using NpgsqlTypes;
using System.Data.Common;
using System.Text.Json;

namespace CapaDatos.util
{
    public static class clsPostgresUtil
    {
        public static void Agregar(NpgsqlCommand cmd, string nombre, NpgsqlDbType tipo, object? valor)
        {
            cmd.Parameters.Add(new NpgsqlParameter(nombre, tipo) { Value = valor ?? DBNull.Value });
        }

        public static string ADiccionarioJson(Dictionary<string, string>? valores)
        {
            return JsonSerializer.Serialize(valores ?? new Dictionary<string, string>());
        }

        public static Dictionary<string, string> LeerDiccionario(DbDataReader reader, string columna)
        {
            int ordinal = reader.GetOrdinal(columna);
            if (reader.IsDBNull(ordinal))
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(ordinal))
                ?? new Dictionary<string, string>();
        }

        public static string? LeerTexto(DbDataReader reader, string columna)
        {
            int ordinal = reader.GetOrdinal(columna);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        public static Guid? LeerGuid(DbDataReader reader, string columna)
        {
            int ordinal = reader.GetOrdinal(columna);
            return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
        }

        public static decimal? LeerDecimal(DbDataReader reader, string columna)
        {
            int ordinal = reader.GetOrdinal(columna);
            return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
        }

        public static async Task<T> TraducirConflictos<T>(Func<Task<T>> operacion)
        {
            try
            {
                return await operacion();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new ConflictoDatosException(ex.ConstraintName, ex);
            }
        }
    }
}
