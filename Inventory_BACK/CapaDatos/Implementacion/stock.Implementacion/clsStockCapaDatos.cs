using CapaDatos.Interfaz.stock.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace CapaDatos.Implementacion.stock.Implementacion
{
    public class clsStockCapaDatos : IStockCapaDatos
    {
        private const int ErrorClaveDuplicada = 2627;
        private const int ErrorIndiceUnico = 2601;

        private readonly IConfiguration _configuration;

        public clsStockCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection NuevaConexion() => new SqlConnection(_configuration.GetConnectionString("SQLConnectionString"));

        public async Task<List<DisponibilidadDto>> ObtenerDisponibilidad(IReadOnlyList<string> skus)
        {
            var resultado = skus.ToDictionary(s => s, s => new DisponibilidadDto { Sku = s }, StringComparer.OrdinalIgnoreCase);
            if (skus.Count == 0)
                return resultado.Values.ToList();

            string parametros = string.Join(", ", skus.Select((_, i) => $"@sku{i}"));
            string query = $"SELECT Sku, CantidadDisponible, StockMinimo FROM inv.Stock WHERE Sku IN ({parametros})";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            for (int i = 0; i < skus.Count; i++)
                cmd.Parameters.Add($"@sku{i}", SqlDbType.NVarChar, 50).Value = skus[i];

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DisponibilidadDto item = resultado[reader.GetString(0)];
                int disponible = reader.GetInt32(1);
                item.Existe = true;
                item.Disponible = Math.Max(disponible, 0);
                item.PocasUnidades = disponible > 0 && disponible <= reader.GetInt32(2);
            }

            return skus.Select(s => resultado[s]).ToList();
        }

        public async Task<StockDto?> ObtenerStock(string sku)
        {
            const string query = @"
                SELECT Sku, ProductoId, VarianteId, CantidadFisica, CantidadReservada, CantidadDisponible, StockMinimo, UpdatedAt
                FROM inv.Stock
                WHERE Sku = @Sku";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 50).Value = sku;
            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapearStock(reader) : null;
        }

        public async Task<(List<StockDto> Items, int Total)> ListarStock(FiltroStockRequest filtro)
        {
            const string query = @"
                SELECT Sku, ProductoId, VarianteId, CantidadFisica, CantidadReservada, CantidadDisponible, StockMinimo, UpdatedAt,
                       COUNT(*) OVER () AS Total
                FROM inv.Stock
                WHERE (@Sku IS NULL OR Sku LIKE @Sku + '%' ESCAPE '\')
                  AND (@SoloBajoMinimo = 0 OR CantidadDisponible <= StockMinimo)
                  AND (@SoloAgotados = 0 OR CantidadDisponible <= 0)
                ORDER BY Sku
                OFFSET @Desplazamiento ROWS FETCH NEXT @Tamano ROWS ONLY";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            string? prefijo = string.IsNullOrWhiteSpace(filtro.sku)
                ? null
                : filtro.sku.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");
            cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 120).Value = (object?)prefijo ?? DBNull.Value;
            cmd.Parameters.Add("@SoloBajoMinimo", SqlDbType.Bit).Value = filtro.soloBajoMinimo;
            cmd.Parameters.Add("@SoloAgotados", SqlDbType.Bit).Value = filtro.soloAgotados;
            cmd.Parameters.Add("@Desplazamiento", SqlDbType.Int).Value = (filtro.pagina - 1) * filtro.tamano;
            cmd.Parameters.Add("@Tamano", SqlDbType.Int).Value = filtro.tamano;

            var items = new List<StockDto>();
            int total = 0;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapearStock(reader));
                total = reader.GetInt32(reader.GetOrdinal("Total"));
            }
            return (items, total);
        }

        public async Task<List<MovimientoDto>> ListarMovimientos(string sku, int limite)
        {
            const string query = @"
                SELECT TOP (@Limite) MovimientoId, Sku, Tipo, Cantidad, FisicaResultante, ReservadaResultante,
                       ReferenciaId, Motivo, UsuarioId, CreatedAt
                FROM inv.Movimientos
                WHERE Sku = @Sku
                ORDER BY MovimientoId DESC";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 50).Value = sku;
            cmd.Parameters.Add("@Limite", SqlDbType.Int).Value = limite;

            var movimientos = new List<MovimientoDto>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                movimientos.Add(new MovimientoDto
                {
                    MovimientoId = reader.GetInt64(0),
                    Sku = reader.GetString(1),
                    Tipo = reader.GetString(2),
                    Cantidad = reader.GetInt32(3),
                    FisicaResultante = reader.GetInt32(4),
                    ReservadaResultante = reader.GetInt32(5),
                    ReferenciaId = reader.IsDBNull(6) ? null : reader.GetGuid(6),
                    Motivo = reader.IsDBNull(7) ? null : reader.GetString(7),
                    UsuarioId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    CreatedAt = reader.GetDateTime(9)
                });
            }
            return movimientos;
        }

        public async Task<bool> RegistrarSku(string sku, Guid? productoId, Guid? varianteId, Guid? usuarioId, string motivo)
        {
            const string query = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                INSERT INTO inv.Stock (Sku, ProductoId, VarianteId)
                VALUES (@Sku, @ProductoId, @VarianteId);

                INSERT INTO inv.Movimientos (Sku, Tipo, Cantidad, FisicaResultante, ReservadaResultante, Motivo, UsuarioId)
                VALUES (@Sku, 'Alta', 0, 0, 0, @Motivo, @UsuarioId);

                COMMIT;";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 50).Value = sku;
            cmd.Parameters.Add("@ProductoId", SqlDbType.UniqueIdentifier).Value = (object?)productoId ?? DBNull.Value;
            cmd.Parameters.Add("@VarianteId", SqlDbType.UniqueIdentifier).Value = (object?)varianteId ?? DBNull.Value;
            cmd.Parameters.Add("@Motivo", SqlDbType.NVarChar, 250).Value = motivo;
            cmd.Parameters.Add("@UsuarioId", SqlDbType.UniqueIdentifier).Value = (object?)usuarioId ?? DBNull.Value;

            try
            {
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (SqlException ex) when (ex.Number is ErrorClaveDuplicada or ErrorIndiceUnico)
            {
                return false;
            }
        }

        public async Task<bool> RegistrarSkuDesdeEvento(Guid mensajeId, string tipoMensaje, string sku, Guid productoId, Guid varianteId)
        {
            const string query = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                INSERT INTO inv.MensajesProcesados (MensajeId, TipoMensaje)
                VALUES (@MensajeId, @TipoMensaje);

                IF EXISTS (SELECT 1 FROM inv.Stock WITH (UPDLOCK, HOLDLOCK) WHERE Sku = @Sku)
                    UPDATE inv.Stock
                    SET ProductoId = COALESCE(ProductoId, @ProductoId),
                        VarianteId = COALESCE(VarianteId, @VarianteId),
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE Sku = @Sku;
                ELSE
                BEGIN
                    INSERT INTO inv.Stock (Sku, ProductoId, VarianteId)
                    VALUES (@Sku, @ProductoId, @VarianteId);

                    INSERT INTO inv.Movimientos (Sku, Tipo, Cantidad, FisicaResultante, ReservadaResultante, ReferenciaId, Motivo)
                    VALUES (@Sku, 'Alta', 0, 0, 0, @MensajeId, N'Variante creada en Catálogo');
                END

                COMMIT;";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@MensajeId", SqlDbType.UniqueIdentifier).Value = mensajeId;
            cmd.Parameters.Add("@TipoMensaje", SqlDbType.VarChar, 100).Value = tipoMensaje;
            cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 50).Value = sku;
            cmd.Parameters.Add("@ProductoId", SqlDbType.UniqueIdentifier).Value = productoId;
            cmd.Parameters.Add("@VarianteId", SqlDbType.UniqueIdentifier).Value = varianteId;

            try
            {
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (SqlException ex) when (ex.Number is ErrorClaveDuplicada or ErrorIndiceUnico)
            {
                return false;
            }
        }

        public async Task<bool> AplicarMovimientoFisico(string sku, int cantidad, string tipo, string motivo, Guid? usuarioId)
        {
            const string query = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                DECLARE @Resultado TABLE (Fisica INT, Reservada INT);

                UPDATE inv.Stock
                SET CantidadFisica = CantidadFisica + @Cantidad,
                    UpdatedAt = SYSUTCDATETIME()
                OUTPUT inserted.CantidadFisica, inserted.CantidadReservada INTO @Resultado
                WHERE Sku = @Sku AND CantidadFisica + @Cantidad >= CantidadReservada;

                INSERT INTO inv.Movimientos (Sku, Tipo, Cantidad, FisicaResultante, ReservadaResultante, Motivo, UsuarioId)
                SELECT @Sku, @Tipo, @Cantidad, Fisica, Reservada, @Motivo, @UsuarioId
                FROM @Resultado;

                COMMIT;

                SELECT COUNT(*) FROM @Resultado;";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 50).Value = sku;
            cmd.Parameters.Add("@Cantidad", SqlDbType.Int).Value = cantidad;
            cmd.Parameters.Add("@Tipo", SqlDbType.VarChar, 20).Value = tipo;
            cmd.Parameters.Add("@Motivo", SqlDbType.NVarChar, 250).Value = motivo;
            cmd.Parameters.Add("@UsuarioId", SqlDbType.UniqueIdentifier).Value = (object?)usuarioId ?? DBNull.Value;
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<bool> ActualizarStockMinimo(string sku, int stockMinimo)
        {
            const string query = "UPDATE inv.Stock SET StockMinimo = @StockMinimo, UpdatedAt = SYSUTCDATETIME() WHERE Sku = @Sku";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@StockMinimo", SqlDbType.Int).Value = stockMinimo;
            cmd.Parameters.Add("@Sku", SqlDbType.NVarChar, 50).Value = sku;
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static StockDto MapearStock(SqlDataReader reader)
        {
            return new StockDto
            {
                Sku = reader.GetString(reader.GetOrdinal("Sku")),
                ProductoId = reader.IsDBNull(reader.GetOrdinal("ProductoId")) ? null : reader.GetGuid(reader.GetOrdinal("ProductoId")),
                VarianteId = reader.IsDBNull(reader.GetOrdinal("VarianteId")) ? null : reader.GetGuid(reader.GetOrdinal("VarianteId")),
                CantidadFisica = reader.GetInt32(reader.GetOrdinal("CantidadFisica")),
                CantidadReservada = reader.GetInt32(reader.GetOrdinal("CantidadReservada")),
                CantidadDisponible = reader.GetInt32(reader.GetOrdinal("CantidadDisponible")),
                StockMinimo = reader.GetInt32(reader.GetOrdinal("StockMinimo")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            };
        }
    }
}
