using CapaDatos.Interfaz.auth.interfaz;
using CapaDto.Respuestas;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace CapaDatos.Implementacion.auth.Implementacion
{
    public class clsAuthCapaDatos : IAuthCapaDatos
    {
        private readonly IConfiguration _configuration;

        public clsAuthCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection NuevaConexion() => new SqlConnection(_configuration.GetConnectionString("SQLConnectionString"));

        public async Task<UsuarioCredencialDto?> ObtenerUsuarioPorEmail(string emailNormalizado)
        {
            const string query = @"
                SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.IsActive, u.IsEmailVerified,
                       c.PasswordHash, c.FailedLoginAttempts, c.LockoutEnd
                FROM dbo.Users u
                INNER JOIN dbo.UserCredentials c ON c.UserId = u.UserId
                WHERE u.NormalizedEmail = @NormalizedEmail";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar, 254).Value = emailNormalizado;
            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapearUsuario(reader) : null;
        }

        public async Task<UsuarioCredencialDto?> ObtenerUsuarioPorId(Guid userId)
        {
            const string query = @"
                SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.IsActive, u.IsEmailVerified,
                       c.PasswordHash, c.FailedLoginAttempts, c.LockoutEnd
                FROM dbo.Users u
                INNER JOIN dbo.UserCredentials c ON c.UserId = u.UserId
                WHERE u.UserId = @UserId";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapearUsuario(reader) : null;
        }

        public async Task<bool> ExisteEmail(string emailNormalizado)
        {
            const string query = "SELECT COUNT(1) FROM dbo.Users WHERE NormalizedEmail = @NormalizedEmail";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar, 254).Value = emailNormalizado;
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<Guid?> CrearUsuario(string nombre, string apellido, string email, string emailNormalizado, string passwordHash, string rolNormalizado)
        {
            const string query = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                DECLARE @Nuevo TABLE (UserId UNIQUEIDENTIFIER);

                INSERT INTO dbo.Users (FirstName, LastName, Email, NormalizedEmail)
                OUTPUT inserted.UserId INTO @Nuevo
                VALUES (@FirstName, @LastName, @Email, @NormalizedEmail);

                DECLARE @UserId UNIQUEIDENTIFIER = (SELECT UserId FROM @Nuevo);

                INSERT INTO dbo.UserCredentials (UserId, PasswordHash)
                VALUES (@UserId, @PasswordHash);

                INSERT INTO dbo.UserRoles (UserId, RoleId)
                SELECT @UserId, RoleId FROM dbo.Roles WHERE NormalizedName = @RolNormalizado;

                IF @@ROWCOUNT = 0
                    THROW 50001, 'El rol por defecto no existe en dbo.Roles.', 1;

                COMMIT TRANSACTION;

                SELECT @UserId;";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@FirstName", SqlDbType.NVarChar, 100).Value = nombre;
            cmd.Parameters.Add("@LastName", SqlDbType.NVarChar, 100).Value = apellido;
            cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email;
            cmd.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar, 254).Value = emailNormalizado;
            cmd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = passwordHash;
            cmd.Parameters.Add("@RolNormalizado", SqlDbType.NVarChar, 50).Value = rolNormalizado;

            try
            {
                return (Guid)(await cmd.ExecuteScalarAsync())!;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return null;
            }
        }

        public async Task<List<string>> ObtenerRolesUsuario(Guid userId)
        {
            const string query = @"
                SELECT r.Name
                FROM dbo.UserRoles ur
                INNER JOIN dbo.Roles r ON r.RoleId = ur.RoleId
                WHERE ur.UserId = @UserId
                ORDER BY r.Name";

            var roles = new List<string>();

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                roles.Add(reader.GetString(0));

            return roles;
        }

        public async Task<DateTime?> RegistrarIntentoFallido(Guid userId, int maxIntentos, int minutosBloqueo)
        {
            const string query = @"
                UPDATE dbo.UserCredentials
                SET FailedLoginAttempts = CASE WHEN FailedLoginAttempts + 1 >= @MaxIntentos THEN 0 ELSE FailedLoginAttempts + 1 END,
                    LockoutEnd = CASE WHEN FailedLoginAttempts + 1 >= @MaxIntentos THEN DATEADD(MINUTE, @MinutosBloqueo, SYSUTCDATETIME()) ELSE LockoutEnd END
                OUTPUT inserted.LockoutEnd
                WHERE UserId = @UserId";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.Parameters.Add("@MaxIntentos", SqlDbType.Int).Value = maxIntentos;
            cmd.Parameters.Add("@MinutosBloqueo", SqlDbType.Int).Value = minutosBloqueo;
            object? resultado = await cmd.ExecuteScalarAsync();
            return resultado is DateTime fecha ? fecha : null;
        }

        public async Task ReiniciarIntentosFallidos(Guid userId)
        {
            const string query = @"
                UPDATE dbo.UserCredentials
                SET FailedLoginAttempts = 0, LockoutEnd = NULL
                WHERE UserId = @UserId";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ActualizarPassword(Guid userId, string passwordHash)
        {
            const string query = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                UPDATE dbo.UserCredentials
                SET PasswordHash = @PasswordHash,
                    PasswordChangedAt = SYSUTCDATETIME(),
                    FailedLoginAttempts = 0,
                    LockoutEnd = NULL
                WHERE UserId = @UserId;

                UPDATE dbo.Users SET UpdatedAt = SYSUTCDATETIME() WHERE UserId = @UserId;

                COMMIT TRANSACTION;";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = passwordHash;
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task GuardarRefreshToken(Guid userId, byte[] tokenHash, Guid familiaId, DateTime expiracion, string? ip)
        {
            const string query = @"
                INSERT INTO dbo.RefreshTokens (UserId, TokenHash, TokenFamilyId, ExpiresAt, CreatedByIp)
                VALUES (@UserId, @TokenHash, @TokenFamilyId, @ExpiresAt, @CreatedByIp)";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.Parameters.Add("@TokenHash", SqlDbType.VarBinary, 32).Value = tokenHash;
            cmd.Parameters.Add("@TokenFamilyId", SqlDbType.UniqueIdentifier).Value = familiaId;
            cmd.Parameters.Add("@ExpiresAt", SqlDbType.DateTime2).Value = expiracion;
            cmd.Parameters.Add("@CreatedByIp", SqlDbType.NVarChar, 45).Value = (object?)ip ?? DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<RefreshTokenDto?> ObtenerRefreshToken(byte[] tokenHash)
        {
            const string query = @"
                SELECT RefreshTokenId, UserId, TokenFamilyId, ExpiresAt, RevokedAt, ReplacedByTokenId
                FROM dbo.RefreshTokens
                WHERE TokenHash = @TokenHash";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@TokenHash", SqlDbType.VarBinary, 32).Value = tokenHash;
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new RefreshTokenDto
            {
                RefreshTokenId = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                TokenFamilyId = reader.GetGuid(2),
                ExpiresAt = reader.GetDateTime(3),
                RevokedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                ReplacedByTokenId = reader.IsDBNull(5) ? null : reader.GetGuid(5)
            };
        }

        public async Task<bool> RotarRefreshToken(Guid refreshTokenIdActual, Guid userId, byte[] nuevoTokenHash, Guid familiaId, DateTime expiracion, string? ip)
        {
            const string query = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                UPDATE dbo.RefreshTokens
                SET RevokedAt = SYSUTCDATETIME(), RevokedByIp = @Ip
                WHERE RefreshTokenId = @IdActual AND RevokedAt IS NULL;

                IF @@ROWCOUNT = 0
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT CAST(0 AS BIT);
                    RETURN;
                END

                DECLARE @Nuevo TABLE (RefreshTokenId UNIQUEIDENTIFIER);

                INSERT INTO dbo.RefreshTokens (UserId, TokenHash, TokenFamilyId, ExpiresAt, CreatedByIp)
                OUTPUT inserted.RefreshTokenId INTO @Nuevo
                VALUES (@UserId, @TokenHash, @TokenFamilyId, @ExpiresAt, @Ip);

                UPDATE dbo.RefreshTokens
                SET ReplacedByTokenId = (SELECT RefreshTokenId FROM @Nuevo)
                WHERE RefreshTokenId = @IdActual;

                COMMIT TRANSACTION;

                SELECT CAST(1 AS BIT);";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@IdActual", SqlDbType.UniqueIdentifier).Value = refreshTokenIdActual;
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.Parameters.Add("@TokenHash", SqlDbType.VarBinary, 32).Value = nuevoTokenHash;
            cmd.Parameters.Add("@TokenFamilyId", SqlDbType.UniqueIdentifier).Value = familiaId;
            cmd.Parameters.Add("@ExpiresAt", SqlDbType.DateTime2).Value = expiracion;
            cmd.Parameters.Add("@Ip", SqlDbType.NVarChar, 45).Value = (object?)ip ?? DBNull.Value;
            return (bool)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task<bool> RevocarRefreshToken(Guid refreshTokenId, string? ip)
        {
            const string query = @"
                UPDATE dbo.RefreshTokens
                SET RevokedAt = SYSUTCDATETIME(), RevokedByIp = @Ip
                WHERE RefreshTokenId = @RefreshTokenId AND RevokedAt IS NULL";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@RefreshTokenId", SqlDbType.UniqueIdentifier).Value = refreshTokenId;
            cmd.Parameters.Add("@Ip", SqlDbType.NVarChar, 45).Value = (object?)ip ?? DBNull.Value;
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task RevocarFamiliaTokens(Guid familiaId, string? ip)
        {
            const string query = @"
                UPDATE dbo.RefreshTokens
                SET RevokedAt = SYSUTCDATETIME(), RevokedByIp = @Ip
                WHERE TokenFamilyId = @TokenFamilyId AND RevokedAt IS NULL";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@TokenFamilyId", SqlDbType.UniqueIdentifier).Value = familiaId;
            cmd.Parameters.Add("@Ip", SqlDbType.NVarChar, 45).Value = (object?)ip ?? DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RevocarTokensUsuario(Guid userId, string? ip)
        {
            const string query = @"
                UPDATE dbo.RefreshTokens
                SET RevokedAt = SYSUTCDATETIME(), RevokedByIp = @Ip
                WHERE UserId = @UserId AND RevokedAt IS NULL";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.Parameters.Add("@Ip", SqlDbType.NVarChar, 45).Value = (object?)ip ?? DBNull.Value;
            await cmd.ExecuteNonQueryAsync();
        }

        private static UsuarioCredencialDto MapearUsuario(SqlDataReader reader)
        {
            return new UsuarioCredencialDto
            {
                UserId = reader.GetGuid(0),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                Email = reader.GetString(3),
                IsActive = reader.GetBoolean(4),
                IsEmailVerified = reader.GetBoolean(5),
                PasswordHash = reader.GetString(6),
                FailedLoginAttempts = reader.GetInt32(7),
                LockoutEnd = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            };
        }
    }
}
