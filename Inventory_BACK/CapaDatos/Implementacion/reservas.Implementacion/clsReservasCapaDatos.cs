using CapaDatos.Interfaz.reservas.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace CapaDatos.Implementacion.reservas.Implementacion
{
    public class clsReservasCapaDatos : IReservasCapaDatos
    {
        private const int ErrorClaveDuplicada = 2627;
        private const int ErrorIndiceUnico = 2601;
        private const string EstadoActiva = "Activa";

        private readonly IConfiguration _configuration;

        public clsReservasCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection NuevaConexion() => new SqlConnection(_configuration.GetConnectionString("SQLConnectionString"));

        public async Task<ReservaDto?> ObtenerReserva(Guid pedidoId)
        {
            const string query = @"
                SELECT ReservaId, PedidoId, Estado, ExpiraEn, CreatedAt
                FROM inv.Reservas
                WHERE PedidoId = @PedidoId;

                SELECT i.Sku, i.Cantidad
                FROM inv.ReservaItems i
                INNER JOIN inv.Reservas r ON r.ReservaId = i.ReservaId
                WHERE r.PedidoId = @PedidoId
                ORDER BY i.Sku;";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = pedidoId;
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var reserva = new ReservaDto
            {
                ReservaId = reader.GetGuid(0),
                PedidoId = reader.GetGuid(1),
                Estado = reader.GetString(2),
                ExpiraEn = reader.GetDateTime(3),
                CreatedAt = reader.GetDateTime(4)
            };

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
                reserva.Items.Add(new ItemReservaDto { Sku = reader.GetString(0), Cantidad = reader.GetInt32(1) });

            return reserva;
        }

        public async Task<(ReservaDto? Reserva, List<FaltanteDto> Faltantes, bool YaExistia)> CrearReserva(
            Guid pedidoId, IReadOnlyList<ItemReservaRequest> items, DateTime expiraEn)
        {
            const string queryReserva = @"
                INSERT INTO inv.Reservas (PedidoId, ExpiraEn)
                OUTPUT inserted.ReservaId
                VALUES (@PedidoId, @ExpiraEn)";

            const string queryItem = @"
                DECLARE @Resultado TABLE (Fisica INT, Reservada INT);

                UPDATE inv.Stock
                SET CantidadReservada = CantidadReservada + @Cantidad,
                    UpdatedAt = SYSUTCDATETIME()
                OUTPUT inserted.CantidadFisica, inserted.CantidadReservada INTO @Resultado
                WHERE Sku = @Sku AND CantidadDisponible >= @Cantidad;

                IF EXISTS (SELECT 1 FROM @Resultado)
                BEGIN
                    INSERT INTO inv.ReservaItems (ReservaId, Sku, Cantidad)
                    VALUES (@ReservaId, @Sku, @Cantidad);

                    INSERT INTO inv.Movimientos (Sku, Tipo, Cantidad, FisicaResultante, ReservadaResultante, ReferenciaId, Motivo)
                    SELECT @Sku, 'Reserva', @Cantidad, Fisica, Reservada, @PedidoId, N'Reserva para pedido'
                    FROM @Resultado;
                END

                SELECT CASE WHEN EXISTS (SELECT 1 FROM @Resultado) THEN -1
                            ELSE ISNULL((SELECT CantidadDisponible FROM inv.Stock WHERE Sku = @Sku), 0) END;";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            Guid reservaId;
            try
            {
                using var cmd = new SqlCommand(queryReserva, connection, transaction);
                cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = pedidoId;
                cmd.Parameters.Add("@ExpiraEn", SqlDbType.DateTime2).Value = expiraEn;
                reservaId = (Guid)(await cmd.ExecuteScalarAsync())!;
            }
            catch (SqlException ex) when (ex.Number is ErrorClaveDuplicada or ErrorIndiceUnico)
            {
                await transaction.RollbackAsync();
                return (await ObtenerReserva(pedidoId), new List<FaltanteDto>(), true);
            }

            var faltantes = new List<FaltanteDto>();
            foreach (ItemReservaRequest item in items.OrderBy(i => i.sku, StringComparer.Ordinal))
            {
                using var cmd = new SqlCommand(queryItem, connection, transaction);
                cmd.Parameters.Add("@ReservaId", SqlDbType.UniqueIdentifier).Value = reservaId;
                cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = pedidoId;
                cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 50).Value = item.sku;
                cmd.Parameters.Add("@Cantidad", SqlDbType.Int).Value = item.cantidad;

                int disponible = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (disponible >= 0)
                    faltantes.Add(new FaltanteDto { Sku = item.sku, Solicitado = item.cantidad, Disponible = disponible });
            }

            if (faltantes.Count > 0)
            {
                await transaction.RollbackAsync();
                return (null, faltantes, false);
            }

            await transaction.CommitAsync();
            return (await ObtenerReserva(pedidoId), faltantes, false);
        }

        public async Task<ResultadoCierreReserva> CerrarReserva(Guid pedidoId, bool vender, string estadoFinal, string tipoMovimiento)
        {
            const string queryBloquear = @"
                SELECT ReservaId, Estado, CASE WHEN ExpiraEn < SYSUTCDATETIME() THEN 1 ELSE 0 END AS Vencida
                FROM inv.Reservas WITH (UPDLOCK, HOLDLOCK)
                WHERE PedidoId = @PedidoId";

            const string queryItems = "SELECT Sku, Cantidad FROM inv.ReservaItems WHERE ReservaId = @ReservaId ORDER BY Sku";

            const string queryItem = @"
                DECLARE @Resultado TABLE (Fisica INT, Reservada INT);

                UPDATE inv.Stock
                SET CantidadReservada = CantidadReservada - @Cantidad,
                    CantidadFisica = CantidadFisica - CASE WHEN @Vender = 1 THEN @Cantidad ELSE 0 END,
                    UpdatedAt = SYSUTCDATETIME()
                OUTPUT inserted.CantidadFisica, inserted.CantidadReservada INTO @Resultado
                WHERE Sku = @Sku;

                INSERT INTO inv.Movimientos (Sku, Tipo, Cantidad, FisicaResultante, ReservadaResultante, ReferenciaId, Motivo)
                SELECT @Sku, @Tipo, -@Cantidad, Fisica, Reservada, @PedidoId, @Motivo
                FROM @Resultado;";

            const string queryEstado = "UPDATE inv.Reservas SET Estado = @Estado, UpdatedAt = SYSUTCDATETIME() WHERE ReservaId = @ReservaId";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            Guid reservaId;
            string estadoActual;
            bool vencida;
            using (var cmd = new SqlCommand(queryBloquear, connection, transaction))
            {
                cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = pedidoId;
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    reader.Close();
                    await transaction.RollbackAsync();
                    return new ResultadoCierreReserva { Encontrada = false };
                }
                reservaId = reader.GetGuid(0);
                estadoActual = reader.GetString(1);
                vencida = reader.GetInt32(2) == 1;
            }

            if (estadoActual != EstadoActiva)
            {
                await transaction.CommitAsync();
                return new ResultadoCierreReserva { Encontrada = true, EstadoAnterior = estadoActual, EstadoFinal = estadoActual };
            }

            if (vender && vencida)
            {
                vender = false;
                estadoFinal = "Expirada";
                tipoMovimiento = "Expiracion";
            }

            var items = new List<(string Sku, int Cantidad)>();
            using (var cmd = new SqlCommand(queryItems, connection, transaction))
            {
                cmd.Parameters.Add("@ReservaId", SqlDbType.UniqueIdentifier).Value = reservaId;
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    items.Add((reader.GetString(0), reader.GetInt32(1)));
            }

            string motivo = tipoMovimiento switch
            {
                "Venta" => "Venta confirmada",
                "Expiracion" => "Reserva vencida sin pago",
                _ => "Reserva liberada"
            };

            foreach (var (sku, cantidad) in items)
            {
                using var cmd = new SqlCommand(queryItem, connection, transaction);
                cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 50).Value = sku;
                cmd.Parameters.Add("@Cantidad", SqlDbType.Int).Value = cantidad;
                cmd.Parameters.Add("@Vender", SqlDbType.Bit).Value = vender;
                cmd.Parameters.Add("@Tipo", SqlDbType.VarChar, 20).Value = tipoMovimiento;
                cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = pedidoId;
                cmd.Parameters.Add("@Motivo", SqlDbType.NVarChar, 250).Value = motivo;
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = new SqlCommand(queryEstado, connection, transaction))
            {
                cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = estadoFinal;
                cmd.Parameters.Add("@ReservaId", SqlDbType.UniqueIdentifier).Value = reservaId;
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return new ResultadoCierreReserva { Encontrada = true, EstadoAnterior = EstadoActiva, EstadoFinal = estadoFinal };
        }

        public async Task<List<Guid>> ObtenerPedidosConReservaVencida(int limite)
        {
            const string query = @"
                SELECT TOP (@Limite) PedidoId
                FROM inv.Reservas
                WHERE Estado = 'Activa' AND ExpiraEn < SYSUTCDATETIME()
                ORDER BY ExpiraEn";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Limite", SqlDbType.Int).Value = limite;

            var pedidos = new List<Guid>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                pedidos.Add(reader.GetGuid(0));
            return pedidos;
        }
    }
}
