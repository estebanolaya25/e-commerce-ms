using CapaDatos.Interfaz.pedidos.interfaz;
using CapaDatos.util;
using CapaDto.Modelos;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace CapaDatos.Implementacion.pedidos.Implementacion
{
    public class clsPedidosCapaDatos : IPedidosCapaDatos
    {
        private const string ColumnasPedido = @"
            PedidoId, Numero, UsuarioId, UsuarioEmail, Estado, Subtotal, CostoEnvio, Total, Moneda,
            DestinatarioNombre, DestinatarioTelefono, Direccion, Ciudad, Departamento, NotasEntrega,
            PagoId, UltimoErrorPago, ExpiraEn, CreatedAt, UpdatedAt";

        private readonly IConfiguration _configuration;

        public clsPedidosCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection NuevaConexion() => new SqlConnection(_configuration.GetConnectionString("SQLConnectionString"));

        public async Task<long> ObtenerSiguienteNumero()
        {
            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand("SELECT NEXT VALUE FOR ped.SecuenciaPedidos", connection);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }

        public async Task<bool> CrearPedido(NuevoPedido pedido, EventoOutbox evento)
        {
            const string queryPedido = @"
                INSERT INTO ped.Pedidos
                    (PedidoId, Numero, UsuarioId, UsuarioEmail, IdempotencyKey, Estado, Subtotal, CostoEnvio, Total,
                     DestinatarioNombre, DestinatarioTelefono, Direccion, Ciudad, Departamento, NotasEntrega, ExpiraEn)
                VALUES
                    (@PedidoId, @Numero, @UsuarioId, @UsuarioEmail, @IdempotencyKey, 'PendientePago', @Subtotal, @CostoEnvio, @Total,
                     @DestinatarioNombre, @DestinatarioTelefono, @Direccion, @Ciudad, @Departamento, @NotasEntrega, @ExpiraEn);

                INSERT INTO ped.PedidoHistorial (PedidoId, EstadoAnterior, EstadoNuevo, Motivo, UsuarioId)
                VALUES (@PedidoId, NULL, 'PendientePago', N'Pedido creado; stock reservado', @UsuarioId);";

            const string queryItem = @"
                INSERT INTO ped.PedidoItems (PedidoId, Sku, Nombre, Opciones, ImagenUrl, PrecioUnitario, Cantidad, Subtotal)
                VALUES (@PedidoId, @Sku, @Nombre, @Opciones, @ImagenUrl, @PrecioUnitario, @Cantidad, @Subtotal)";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                using (var cmd = new SqlCommand(queryPedido, connection, transaction))
                {
                    cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = pedido.PedidoId;
                    cmd.Parameters.Add("@Numero", SqlDbType.VarChar, 20).Value = pedido.Numero;
                    cmd.Parameters.Add("@UsuarioId", SqlDbType.UniqueIdentifier).Value = pedido.UsuarioId;
                    cmd.Parameters.Add("@UsuarioEmail", SqlDbType.NVarChar, 254).Value = (object?)pedido.UsuarioEmail ?? DBNull.Value;
                    cmd.Parameters.Add("@IdempotencyKey", SqlDbType.VarChar, 64).Value = pedido.IdempotencyKey;
                    cmd.Parameters.Add("@Subtotal", SqlDbType.Decimal).Value = pedido.Subtotal;
                    cmd.Parameters.Add("@CostoEnvio", SqlDbType.Decimal).Value = pedido.CostoEnvio;
                    cmd.Parameters.Add("@Total", SqlDbType.Decimal).Value = pedido.Total;
                    cmd.Parameters.Add("@DestinatarioNombre", SqlDbType.NVarChar, 150).Value = pedido.Direccion.NombreDestinatario;
                    cmd.Parameters.Add("@DestinatarioTelefono", SqlDbType.VarChar, 20).Value = pedido.Direccion.Telefono;
                    cmd.Parameters.Add("@Direccion", SqlDbType.NVarChar, 250).Value = pedido.Direccion.Direccion;
                    cmd.Parameters.Add("@Ciudad", SqlDbType.NVarChar, 100).Value = pedido.Direccion.Ciudad;
                    cmd.Parameters.Add("@Departamento", SqlDbType.NVarChar, 100).Value = pedido.Direccion.Departamento;
                    cmd.Parameters.Add("@NotasEntrega", SqlDbType.NVarChar, 300).Value = (object?)pedido.Direccion.Notas ?? DBNull.Value;
                    cmd.Parameters.Add("@ExpiraEn", SqlDbType.DateTime2).Value = pedido.ExpiraEn;
                    await cmd.ExecuteNonQueryAsync();
                }

                foreach (ItemPedidoDto item in pedido.Items)
                {
                    using var cmd = new SqlCommand(queryItem, connection, transaction);
                    cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = pedido.PedidoId;
                    cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 50).Value = item.Sku;
                    cmd.Parameters.Add("@Nombre", SqlDbType.NVarChar, 200).Value = item.Nombre;
                    cmd.Parameters.Add("@Opciones", SqlDbType.NVarChar, 500).Value = (object?)item.Opciones ?? DBNull.Value;
                    cmd.Parameters.Add("@ImagenUrl", SqlDbType.NVarChar, 500).Value = (object?)item.ImagenUrl ?? DBNull.Value;
                    cmd.Parameters.Add("@PrecioUnitario", SqlDbType.Decimal).Value = item.PrecioUnitario;
                    cmd.Parameters.Add("@Cantidad", SqlDbType.Int).Value = item.Cantidad;
                    cmd.Parameters.Add("@Subtotal", SqlDbType.Decimal).Value = item.Subtotal;
                    await cmd.ExecuteNonQueryAsync();
                }

                await clsOutboxSql.Agregar(connection, transaction, evento);
                await transaction.CommitAsync();
                return true;
            }
            catch (SqlException ex) when (ex.Number is clsOutboxSql.ErrorClaveDuplicada or clsOutboxSql.ErrorIndiceUnico)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public Task<PedidoDto?> ObtenerPedido(Guid pedidoId)
        {
            return ObtenerPedidoCompleto("PedidoId = @Valor", cmd => cmd.Parameters.Add("@Valor", SqlDbType.UniqueIdentifier).Value = pedidoId);
        }

        public Task<PedidoDto?> ObtenerPorIdempotencia(Guid usuarioId, string idempotencyKey)
        {
            return ObtenerPedidoCompleto("UsuarioId = @Usuario AND IdempotencyKey = @Valor", cmd =>
            {
                cmd.Parameters.Add("@Usuario", SqlDbType.UniqueIdentifier).Value = usuarioId;
                cmd.Parameters.Add("@Valor", SqlDbType.VarChar, 64).Value = idempotencyKey;
            });
        }

        private async Task<PedidoDto?> ObtenerPedidoCompleto(string filtro, Action<SqlCommand> parametros)
        {
            string query = $@"
                DECLARE @Id UNIQUEIDENTIFIER = (SELECT PedidoId FROM ped.Pedidos WHERE {filtro});

                SELECT {ColumnasPedido} FROM ped.Pedidos WHERE PedidoId = @Id;

                SELECT Sku, Nombre, Opciones, ImagenUrl, PrecioUnitario, Cantidad, Subtotal
                FROM ped.PedidoItems WHERE PedidoId = @Id ORDER BY Nombre, Sku;

                SELECT EstadoAnterior, EstadoNuevo, Motivo, CreatedAt
                FROM ped.PedidoHistorial WHERE PedidoId = @Id ORDER BY HistorialId;";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            parametros(cmd);
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;
            PedidoDto pedido = MapearPedido(reader);

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                pedido.Items.Add(new ItemPedidoDto
                {
                    Sku = reader.GetString(0),
                    Nombre = reader.GetString(1),
                    Opciones = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ImagenUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                    PrecioUnitario = reader.GetDecimal(4),
                    Cantidad = reader.GetInt32(5),
                    Subtotal = reader.GetDecimal(6)
                });
            }

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                pedido.Historial.Add(new HistorialPedidoDto
                {
                    EstadoAnterior = reader.IsDBNull(0) ? null : reader.GetString(0),
                    EstadoNuevo = reader.GetString(1),
                    Motivo = reader.IsDBNull(2) ? null : reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3)
                });
            }

            return pedido;
        }

        public async Task<(List<PedidoResumenDto> Items, int Total)> ListarPedidos(Guid? usuarioId, FiltroPedidosRequest filtro, bool soloDespachables = false)
        {
            const string query = @"
                SELECT p.PedidoId, p.Numero, p.UsuarioId, p.Estado, p.Total, p.ExpiraEn, p.CreatedAt,
                       (SELECT SUM(i.Cantidad) FROM ped.PedidoItems i WHERE i.PedidoId = p.PedidoId) AS Unidades,
                       COUNT(*) OVER () AS TotalFilas
                FROM ped.Pedidos p
                WHERE (@UsuarioId IS NULL OR p.UsuarioId = @UsuarioId)
                  AND (@Estado IS NULL OR p.Estado = @Estado)
                  AND (@Numero IS NULL OR p.Numero = @Numero)
                  AND (@SoloDespachables = 0 OR p.Estado IN ('Pagado', 'EnPreparacion', 'Enviado', 'Entregado'))
                ORDER BY p.CreatedAt DESC
                OFFSET @Desplazamiento ROWS FETCH NEXT @Tamano ROWS ONLY";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UsuarioId", SqlDbType.UniqueIdentifier).Value = (object?)usuarioId ?? DBNull.Value;
            cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = (object?)filtro.estado ?? DBNull.Value;
            cmd.Parameters.Add("@Numero", SqlDbType.VarChar, 20).Value = (object?)filtro.numero?.Trim().ToUpperInvariant() ?? DBNull.Value;
            cmd.Parameters.Add("@SoloDespachables", SqlDbType.Bit).Value = soloDespachables;
            cmd.Parameters.Add("@Desplazamiento", SqlDbType.Int).Value = (filtro.pagina - 1) * filtro.tamano;
            cmd.Parameters.Add("@Tamano", SqlDbType.Int).Value = filtro.tamano;

            var pedidos = new List<PedidoResumenDto>();
            int total = 0;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                pedidos.Add(new PedidoResumenDto
                {
                    PedidoId = reader.GetGuid(0),
                    Numero = reader.GetString(1),
                    UsuarioId = reader.GetGuid(2),
                    Estado = reader.GetString(3),
                    Total = reader.GetDecimal(4),
                    ExpiraEn = reader.GetDateTime(5),
                    CreatedAt = reader.GetDateTime(6),
                    Unidades = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                });
                total = reader.GetInt32(8);
            }
            return (pedidos, total);
        }

        public async Task<ResultadoTransicion> AplicarTransicion(TransicionPedido transicion)
        {
            const string queryEstado = @"
                UPDATE ped.Pedidos
                SET Estado = COALESCE(@NuevoEstado, Estado),
                    PagoId = COALESCE(@PagoId, PagoId),
                    UltimoErrorPago = CASE WHEN @ActualizarErrorPago = 1 THEN @ErrorPago ELSE UltimoErrorPago END,
                    UpdatedAt = SYSUTCDATETIME()
                OUTPUT deleted.Estado, inserted.Estado
                WHERE PedidoId = @PedidoId
                  AND Estado IN (SELECT value FROM STRING_SPLIT(@EstadosPermitidos, ','))";

            const string queryHistorial = @"
                INSERT INTO ped.PedidoHistorial (PedidoId, EstadoAnterior, EstadoNuevo, Motivo, UsuarioId)
                VALUES (@PedidoId, @EstadoAnterior, @EstadoNuevo, @Motivo, @UsuarioId)";

            const string queryMensaje = "INSERT INTO ped.MensajesProcesados (MensajeId, TipoMensaje) VALUES (@MensajeId, @TipoMensaje)";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            if (transicion.MensajeId.HasValue)
            {
                try
                {
                    using var cmd = new SqlCommand(queryMensaje, connection, transaction);
                    cmd.Parameters.Add("@MensajeId", SqlDbType.UniqueIdentifier).Value = transicion.MensajeId.Value;
                    cmd.Parameters.Add("@TipoMensaje", SqlDbType.VarChar, 100).Value = transicion.TipoMensaje ?? "Desconocido";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (SqlException ex) when (ex.Number is clsOutboxSql.ErrorClaveDuplicada or clsOutboxSql.ErrorIndiceUnico)
                {
                    await transaction.RollbackAsync();
                    return ResultadoTransicion.MensajeDuplicado;
                }
            }

            string estadoAnterior;
            string estadoNuevo;
            using (var cmd = new SqlCommand(queryEstado, connection, transaction))
            {
                cmd.Parameters.Add("@NuevoEstado", SqlDbType.VarChar, 20).Value = (object?)transicion.NuevoEstado ?? DBNull.Value;
                cmd.Parameters.Add("@PagoId", SqlDbType.UniqueIdentifier).Value = (object?)transicion.PagoId ?? DBNull.Value;
                cmd.Parameters.Add("@ActualizarErrorPago", SqlDbType.Bit).Value = transicion.ActualizarErrorPago;
                cmd.Parameters.Add("@ErrorPago", SqlDbType.NVarChar, 200).Value = (object?)transicion.ErrorPago ?? DBNull.Value;
                cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = transicion.PedidoId;
                cmd.Parameters.Add("@EstadosPermitidos", SqlDbType.VarChar, 300).Value = string.Join(',', transicion.EstadosPermitidos);
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    reader.Close();
                    await transaction.RollbackAsync();
                    return ResultadoTransicion.EstadoNoPermitido;
                }
                estadoAnterior = reader.GetString(0);
                estadoNuevo = reader.GetString(1);
            }

            using (var cmd = new SqlCommand(queryHistorial, connection, transaction))
            {
                cmd.Parameters.Add("@PedidoId", SqlDbType.UniqueIdentifier).Value = transicion.PedidoId;
                cmd.Parameters.Add("@EstadoAnterior", SqlDbType.VarChar, 20).Value = estadoAnterior;
                cmd.Parameters.Add("@EstadoNuevo", SqlDbType.VarChar, 20).Value = estadoNuevo;
                cmd.Parameters.Add("@Motivo", SqlDbType.NVarChar, 300).Value = (object?)transicion.Motivo ?? DBNull.Value;
                cmd.Parameters.Add("@UsuarioId", SqlDbType.UniqueIdentifier).Value = (object?)transicion.UsuarioId ?? DBNull.Value;
                await cmd.ExecuteNonQueryAsync();
            }

            if (transicion.Evento != null)
                await clsOutboxSql.Agregar(connection, transaction, transicion.Evento);

            await transaction.CommitAsync();
            return ResultadoTransicion.Aplicada;
        }

        public async Task<List<Guid>> ObtenerPedidosVencidos(int limite, TimeSpan graciaPagoEnProceso)
        {
            const string query = @"
                SELECT TOP (@Limite) PedidoId
                FROM ped.Pedidos
                WHERE (Estado = 'PendientePago' AND ExpiraEn < SYSUTCDATETIME())
                   OR (Estado = 'PagoEnProceso' AND ExpiraEn < DATEADD(SECOND, -@GraciaSegundos, SYSUTCDATETIME()))
                ORDER BY ExpiraEn";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Limite", SqlDbType.Int).Value = limite;
            cmd.Parameters.Add("@GraciaSegundos", SqlDbType.Int).Value = (int)graciaPagoEnProceso.TotalSeconds;

            var pedidos = new List<Guid>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                pedidos.Add(reader.GetGuid(0));
            return pedidos;
        }

        private static PedidoDto MapearPedido(SqlDataReader reader)
        {
            return new PedidoDto
            {
                PedidoId = reader.GetGuid(0),
                Numero = reader.GetString(1),
                UsuarioId = reader.GetGuid(2),
                UsuarioEmail = reader.IsDBNull(3) ? null : reader.GetString(3),
                Estado = reader.GetString(4),
                Subtotal = reader.GetDecimal(5),
                CostoEnvio = reader.GetDecimal(6),
                Total = reader.GetDecimal(7),
                Moneda = reader.GetString(8),
                Direccion = new DireccionDto
                {
                    NombreDestinatario = reader.GetString(9),
                    Telefono = reader.GetString(10),
                    Direccion = reader.GetString(11),
                    Ciudad = reader.GetString(12),
                    Departamento = reader.GetString(13),
                    Notas = reader.IsDBNull(14) ? null : reader.GetString(14)
                },
                PagoId = reader.IsDBNull(15) ? null : reader.GetGuid(15),
                UltimoErrorPago = reader.IsDBNull(16) ? null : reader.GetString(16),
                ExpiraEn = reader.GetDateTime(17),
                CreatedAt = reader.GetDateTime(18),
                UpdatedAt = reader.GetDateTime(19)
            };
        }
    }
}
