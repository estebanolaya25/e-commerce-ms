using CapaDatos.Interfaz.pagos.interfaz;
using CapaDto.Modelos;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace CapaDatos.Implementacion.pagos.Implementacion
{
    public class clsPagosCapaDatos : IPagosCapaDatos
    {
        private const int ErrorClaveDuplicada = 2627;
        private const int ErrorIndiceUnico = 2601;
        private const string ColumnasPago = @"
            PagoId, PedidoId, Monto, Moneda, Estado, Marca, Ultimos4, Titular, Referencia, MotivoRechazo,
            CreatedAt, ProcesadoEn, ReembolsadoEn";

        private readonly IConfiguration _configuration;

        public clsPagosCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection NuevaConexion() => new SqlConnection(_configuration.GetConnectionString("SQLConnectionString"));

        public async Task<PagoDto?> CrearPago(NuevoPago pago)
        {
            string query = $@"
                INSERT INTO pag.Pagos (PedidoId, Monto, Marca, Ultimos4, Titular, Modo, ProcesarEn)
                OUTPUT {string.Join(", ", ColumnasPago.Split(',').Select(c => "inserted." + c.Trim()))}
                VALUES (@PedidoId, @Monto, @Marca, @Ultimos4, @Titular, @Modo, @ProcesarEn)";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = pago.PedidoId;
            cmd.Parameters.Add("@Monto", SqlDbType.Decimal).Value = pago.Monto;
            cmd.Parameters.Add("@Marca", SqlDbType.VarChar, 20).Value = pago.Marca;
            cmd.Parameters.Add("@Ultimos4", SqlDbType.Char, 4).Value = pago.Ultimos4;
            cmd.Parameters.Add("@Titular", SqlDbType.NVarChar, 100).Value = pago.Titular;
            cmd.Parameters.Add("@Modo", SqlDbType.VarChar, 10).Value = pago.Modo;
            cmd.Parameters.Add("@ProcesarEn", SqlDbType.DateTime2).Value = pago.ProcesarEn;

            try
            {
                using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                return MapearPago(reader);
            }
            catch (SqlException ex) when (ex.Number is ErrorClaveDuplicada or ErrorIndiceUnico)
            {
                return null;
            }
        }

        public Task<PagoDto?> ObtenerPago(Guid pagoId) =>
            ObtenerUno($"SELECT {ColumnasPago} FROM pag.Pagos WHERE PagoId = @Valor", pagoId);

        public Task<PagoDto?> ObtenerVigentePorPedido(Guid pedidoId) =>
            ObtenerUno($"SELECT {ColumnasPago} FROM pag.Pagos WHERE PedidoId = @Valor AND Estado IN ('Procesando', 'Aprobado')", pedidoId);

        private async Task<PagoDto?> ObtenerUno(string query, Guid valor)
        {
            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Valor", SqlDbType.UniqueIdentifier).Value = valor;
            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapearPago(reader) : null;
        }

        public async Task<(List<PagoDto> Items, int Total)> ListarPagos(FiltroPagosRequest filtro)
        {
            string query = $@"
                SELECT {ColumnasPago}, COUNT(*) OVER () AS TotalFilas
                FROM pag.Pagos
                WHERE (@PedidoId IS NULL OR PedidoId = @PedidoId)
                  AND (@Estado IS NULL OR Estado = @Estado)
                ORDER BY CreatedAt DESC
                OFFSET @Desplazamiento ROWS FETCH NEXT @Tamano ROWS ONLY";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = (object?)filtro.pedidoId ?? DBNull.Value;
            cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = (object?)filtro.estado ?? DBNull.Value;
            cmd.Parameters.Add("@Desplazamiento", SqlDbType.Int).Value = (filtro.pagina - 1) * filtro.tamano;
            cmd.Parameters.Add("@Tamano", SqlDbType.Int).Value = filtro.tamano;

            var pagos = new List<PagoDto>();
            int total = 0;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                pagos.Add(MapearPago(reader));
                total = reader.GetInt32(reader.GetOrdinal("TotalFilas"));
            }
            return (pagos, total);
        }

        public async Task<List<PagoPorProcesar>> ObtenerPorProcesar(int limite)
        {
            const string query = @"
                SELECT TOP (@Limite) PagoId, PedidoId, Monto, Modo
                FROM pag.Pagos
                WHERE Estado = 'Procesando' AND ProcesarEn <= SYSUTCDATETIME()
                ORDER BY ProcesarEn";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Limite", SqlDbType.Int).Value = limite;

            var pagos = new List<PagoPorProcesar>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                pagos.Add(new PagoPorProcesar { PagoId = reader.GetGuid(0), PedidoId = reader.GetGuid(1), Monto = reader.GetDecimal(2), Modo = reader.GetString(3) });
            return pagos;
        }

        public async Task<bool> ResolverPago(Guid pagoId, bool aprobado, string? referencia, string? motivoRechazo, EventoOutbox evento)
        {
            const string query = @"
                UPDATE pag.Pagos
                SET Estado = @Estado, Referencia = @Referencia, MotivoRechazo = @Motivo, ProcesadoEn = SYSUTCDATETIME()
                WHERE PagoId = @PagoId AND Estado = 'Procesando'";

            return await ActualizarConEvento(query, evento, cmd =>
            {
                cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = aprobado ? EstadoPago.Aprobado : EstadoPago.Rechazado;
                cmd.Parameters.Add("@Referencia", SqlDbType.VarChar, 30).Value = (object?)referencia ?? DBNull.Value;
                cmd.Parameters.Add("@Motivo", SqlDbType.NVarChar, 200).Value = (object?)motivoRechazo ?? DBNull.Value;
                cmd.Parameters.Add("@PagoId", SqlDbType.UniqueIdentifier).Value = pagoId;
            });
        }

        public async Task<bool> Reembolsar(Guid pagoId, EventoOutbox evento)
        {
            const string query = @"
                UPDATE pag.Pagos
                SET Estado = 'Reembolsado', ReembolsadoEn = SYSUTCDATETIME()
                WHERE PagoId = @PagoId AND Estado = 'Aprobado'";

            return await ActualizarConEvento(query, evento, cmd =>
                cmd.Parameters.Add("@PagoId", SqlDbType.UniqueIdentifier).Value = pagoId);
        }

        // El cambio de estado y el evento se guardan juntos: o quedan los dos o ninguno (patrón Outbox).
        private async Task<bool> ActualizarConEvento(string query, EventoOutbox evento, Action<SqlCommand> parametros)
        {
            const string queryOutbox = "INSERT INTO pag.Outbox (OutboxId, Tipo, Contenido) VALUES (@OutboxId, @Tipo, @Contenido)";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            using (var cmd = new SqlCommand(query, connection, transaction))
            {
                parametros(cmd);
                if (await cmd.ExecuteNonQueryAsync() == 0)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }

            using (var cmd = new SqlCommand(queryOutbox, connection, transaction))
            {
                cmd.Parameters.Add("@OutboxId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                cmd.Parameters.Add("@Tipo", SqlDbType.VarChar, 100).Value = evento.Tipo;
                cmd.Parameters.Add("@Contenido", SqlDbType.NVarChar, -1).Value = evento.Contenido;
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return true;
        }

        private static PagoDto MapearPago(SqlDataReader reader)
        {
            return new PagoDto
            {
                PagoId = reader.GetGuid(0),
                PedidoId = reader.GetGuid(1),
                Monto = reader.GetDecimal(2),
                Moneda = reader.GetString(3),
                Estado = reader.GetString(4),
                Marca = reader.GetString(5),
                Ultimos4 = reader.GetString(6),
                Titular = reader.GetString(7),
                Referencia = reader.IsDBNull(8) ? null : reader.GetString(8),
                MotivoRechazo = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt = reader.GetDateTime(10),
                ProcesadoEn = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                ReembolsadoEn = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
            };
        }
    }
}
