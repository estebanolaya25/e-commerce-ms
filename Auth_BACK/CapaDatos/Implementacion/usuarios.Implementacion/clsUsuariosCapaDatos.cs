using CapaDatos.Interfaz.usuarios.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace CapaDatos.Implementacion.usuarios.Implementacion
{
    public class clsUsuariosCapaDatos : IUsuariosCapaDatos
    {
        private const string RolAdministrador = "ADMINISTRATOR";

        // Columnas comunes de la lista y el detalle. El último acceso sale de la auditoría
        // (índice IX_AuthAuditLogs_UserId_CreatedAt) y el bloqueo solo cuenta si sigue vigente.
        private const string SelectUsuario = @"
                SELECT u.UserId, u.FirstName, u.LastName, u.Email, u.IsActive, u.CreatedAt,
                       CASE WHEN c.LockoutEnd > SYSUTCDATETIME() THEN c.LockoutEnd END AS BloqueadoHasta,
                       c.FailedLoginAttempts,
                       (SELECT STRING_AGG(r.Name, ',') WITHIN GROUP (ORDER BY r.Name)
                          FROM dbo.UserRoles ur
                          INNER JOIN dbo.Roles r ON r.RoleId = ur.RoleId
                         WHERE ur.UserId = u.UserId) AS Roles,
                       (SELECT MAX(a.CreatedAt)
                          FROM dbo.AuthAuditLogs a
                         WHERE a.UserId = u.UserId AND a.EventType = 'LoginSucceeded') AS UltimoAcceso";

        private readonly IConfiguration _configuration;

        public clsUsuariosCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection NuevaConexion() => new SqlConnection(_configuration.GetConnectionString("SQLConnectionString"));

        public async Task<(List<UsuarioResumenDto> Items, int Total)> ListarUsuarios(FiltroUsuariosRequest filtro)
        {
            string query = SelectUsuario + @",
                       COUNT(*) OVER () AS TotalFilas
                FROM dbo.Users u
                INNER JOIN dbo.UserCredentials c ON c.UserId = u.UserId
                WHERE (@Patron IS NULL
                       OR u.Email LIKE @Patron ESCAPE '\'
                       OR (u.FirstName + N' ' + u.LastName) LIKE @Patron ESCAPE '\')
                  AND (@Rol IS NULL OR EXISTS (
                           SELECT 1 FROM dbo.UserRoles ur
                           INNER JOIN dbo.Roles r ON r.RoleId = ur.RoleId
                           WHERE ur.UserId = u.UserId AND r.NormalizedName = @Rol))
                  AND (@Activo IS NULL OR u.IsActive = @Activo)
                ORDER BY u.CreatedAt DESC, u.Email
                OFFSET @Desplazamiento ROWS FETCH NEXT @Tamano ROWS ONLY";

            string? patron = string.IsNullOrWhiteSpace(filtro.buscar)
                ? null
                : "%" + filtro.buscar.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[") + "%";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Patron", SqlDbType.NVarChar, 260).Value = (object?)patron ?? DBNull.Value;
            cmd.Parameters.Add("@Rol", SqlDbType.NVarChar, 50).Value = (object?)filtro.rol?.Trim().ToUpperInvariant() ?? DBNull.Value;
            cmd.Parameters.Add("@Activo", SqlDbType.Bit).Value = (object?)filtro.activo ?? DBNull.Value;
            cmd.Parameters.Add("@Desplazamiento", SqlDbType.Int).Value = (filtro.pagina - 1) * filtro.tamano;
            cmd.Parameters.Add("@Tamano", SqlDbType.Int).Value = filtro.tamano;

            var usuarios = new List<UsuarioResumenDto>();
            int total = 0;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var usuario = new UsuarioResumenDto();
                MapearUsuario(reader, usuario);
                usuarios.Add(usuario);
                total = reader.GetInt32(10);
            }
            return (usuarios, total);
        }

        public async Task<UsuarioDetalleDto?> ObtenerUsuario(Guid userId)
        {
            string query = SelectUsuario + @"
                FROM dbo.Users u
                INNER JOIN dbo.UserCredentials c ON c.UserId = u.UserId
                WHERE u.UserId = @UserId";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var usuario = new UsuarioDetalleDto { IntentosFallidos = reader.GetInt32(7) };
            MapearUsuario(reader, usuario);
            return usuario;
        }

        public async Task<List<EventoAuditoriaDto>> ListarActividad(Guid userId, int limite)
        {
            const string query = @"
                SELECT TOP (@Limite) EventType, IsSuccessful, IpAddress, Details, CreatedAt
                FROM dbo.AuthAuditLogs
                WHERE UserId = @UserId
                ORDER BY CreatedAt DESC, AuthAuditLogId DESC";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.Parameters.Add("@Limite", SqlDbType.Int).Value = limite;

            var eventos = new List<EventoAuditoriaDto>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                eventos.Add(new EventoAuditoriaDto
                {
                    Tipo = reader.GetString(0),
                    Exitoso = reader.GetBoolean(1),
                    Ip = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Detalle = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Fecha = Utc(reader.GetDateTime(4))
                });
            }
            return eventos;
        }

        public async Task<List<RolDto>> ListarRoles()
        {
            const string query = @"
                SELECT r.Name, r.Description,
                       (SELECT COUNT(*) FROM dbo.UserRoles ur WHERE ur.RoleId = r.RoleId) AS Usuarios
                FROM dbo.Roles r
                ORDER BY r.RoleId";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);

            var roles = new List<RolDto>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                roles.Add(new RolDto
                {
                    Nombre = reader.GetString(0),
                    Descripcion = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Usuarios = reader.GetInt32(2)
                });
            }
            return roles;
        }

        public async Task<int> ContarAdministradoresActivos()
        {
            const string query = @"
                SELECT COUNT(*)
                FROM dbo.Users u
                INNER JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
                INNER JOIN dbo.Roles r ON r.RoleId = ur.RoleId
                WHERE u.IsActive = 1 AND r.NormalizedName = @Rol";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@Rol", SqlDbType.NVarChar, 50).Value = RolAdministrador;
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<Guid?> CrearUsuario(string nombre, string apellido, string email, string emailNormalizado, string passwordHash, IReadOnlyList<string> rolesNormalizados)
        {
            string query = $@"
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
                SELECT @UserId, RoleId FROM dbo.Roles WHERE NormalizedName IN ({ParametrosRoles(rolesNormalizados)});

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
            AgregarRoles(cmd, rolesNormalizados);

            try
            {
                return (Guid)(await cmd.ExecuteScalarAsync())!;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return null;
            }
        }

        // Deja exactamente los roles indicados; los que se mantienen conservan su fecha de asignación.
        public async Task<bool> ReemplazarRoles(Guid userId, IReadOnlyList<string> rolesNormalizados)
        {
            string lista = ParametrosRoles(rolesNormalizados);
            string query = $@"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                IF NOT EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK) WHERE UserId = @UserId)
                BEGIN
                    ROLLBACK TRANSACTION;
                    SELECT CAST(0 AS BIT);
                    RETURN;
                END

                DELETE FROM dbo.UserRoles
                WHERE UserId = @UserId
                  AND RoleId NOT IN (SELECT RoleId FROM dbo.Roles WHERE NormalizedName IN ({lista}));

                INSERT INTO dbo.UserRoles (UserId, RoleId)
                SELECT @UserId, r.RoleId
                FROM dbo.Roles r
                WHERE r.NormalizedName IN ({lista})
                  AND NOT EXISTS (SELECT 1 FROM dbo.UserRoles ur WHERE ur.UserId = @UserId AND ur.RoleId = r.RoleId);

                UPDATE dbo.Users SET UpdatedAt = SYSUTCDATETIME() WHERE UserId = @UserId;

                COMMIT TRANSACTION;
                SELECT CAST(1 AS BIT);";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            AgregarRoles(cmd, rolesNormalizados);
            return (bool)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task<bool> CambiarEstado(Guid userId, bool activo)
        {
            const string query = "UPDATE dbo.Users SET IsActive = @Activo, UpdatedAt = SYSUTCDATETIME() WHERE UserId = @UserId";

            using var connection = NuevaConexion();
            await connection.OpenAsync();
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
            cmd.Parameters.Add("@Activo", SqlDbType.Bit).Value = activo;
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // Un parámetro por rol (@Rol0, @Rol1, ...): la lista ya viene validada contra dbo.Roles.
        private static string ParametrosRoles(IReadOnlyList<string> roles) =>
            string.Join(", ", roles.Select((_, i) => $"@Rol{i}"));

        private static void AgregarRoles(SqlCommand cmd, IReadOnlyList<string> roles)
        {
            for (int i = 0; i < roles.Count; i++)
                cmd.Parameters.Add($"@Rol{i}", SqlDbType.NVarChar, 50).Value = roles[i];
        }

        private static void MapearUsuario(SqlDataReader reader, UsuarioResumenDto usuario)
        {
            usuario.UserId = reader.GetGuid(0);
            usuario.Nombre = reader.GetString(1);
            usuario.Apellido = reader.GetString(2);
            usuario.Email = reader.GetString(3);
            usuario.Activo = reader.GetBoolean(4);
            usuario.CreatedAt = Utc(reader.GetDateTime(5));
            usuario.BloqueadoHasta = reader.IsDBNull(6) ? null : Utc(reader.GetDateTime(6));
            usuario.Roles = reader.IsDBNull(8) ? new List<string>() : reader.GetString(8).Split(',').ToList();
            usuario.UltimoAcceso = reader.IsDBNull(9) ? null : Utc(reader.GetDateTime(9));
        }

        // SQL Server guarda UTC sin zona; así el JSON sale con "Z".
        private static DateTime Utc(DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
    }
}
