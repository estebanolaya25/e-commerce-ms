using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.auth.interfaz
{
    public interface IAuthCapaDatos
    {
        Task<UsuarioCredencialDto?> ObtenerUsuarioPorEmail(string emailNormalizado);
        Task<UsuarioCredencialDto?> ObtenerUsuarioPorId(Guid userId);
        Task<bool> ExisteEmail(string emailNormalizado);
        Task<Guid?> CrearUsuario(string nombre, string apellido, string email, string emailNormalizado, string passwordHash, string rolNormalizado);
        Task<List<string>> ObtenerRolesUsuario(Guid userId);

        Task<DateTime?> RegistrarIntentoFallido(Guid userId, int maxIntentos, int minutosBloqueo);
        Task ReiniciarIntentosFallidos(Guid userId);
        Task ActualizarPassword(Guid userId, string passwordHash);

        Task GuardarRefreshToken(Guid userId, byte[] tokenHash, Guid familiaId, DateTime expiracion, string? ip);
        Task<RefreshTokenDto?> ObtenerRefreshToken(byte[] tokenHash);
        Task<bool> RotarRefreshToken(Guid refreshTokenIdActual, Guid userId, byte[] nuevoTokenHash, Guid familiaId, DateTime expiracion, string? ip);
        Task<bool> RevocarRefreshToken(Guid refreshTokenId, string? ip);
        Task RevocarFamiliaTokens(Guid familiaId, string? ip);
        Task RevocarTokensUsuario(Guid userId, string? ip);
    }
}
