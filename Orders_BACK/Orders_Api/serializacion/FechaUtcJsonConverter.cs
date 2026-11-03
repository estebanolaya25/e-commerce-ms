using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orders_Api.serializacion
{
    // SQL Server guarda las fechas en UTC (SYSUTCDATETIME) pero SqlClient las devuelve sin zona horaria
    // (DateTimeKind.Unspecified), y se serializarían sin la "Z" final: los clientes las leerían como hora local.
    // Este conversor las marca como UTC al escribir el JSON ("2026-10-31T18:15:31Z").
    public class FechaUtcJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            DateTime fecha = reader.GetDateTime();
            return fecha.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(fecha, DateTimeKind.Utc) : fecha.ToUniversalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            DateTime utc = value.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => value
            };
            writer.WriteStringValue(utc);
        }
    }
}
