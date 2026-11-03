using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.auth.interfaz
{
    public interface IAuthCapaNegocios
    {
        Task<ResultadoAuth<AuthResponseDto>> Registrar(RegistroRequest request, string? ip, string? userAgent);
        Task<ResultadoAuth<AuthResponseDto>> Login(LoginRequest request, string? ip, string? userAgent);
        Task<ResultadoAuth<AuthResponseDto>> RefrescarToken(string refreshToken, string? ip, string? userAgent);
        Task<bool> RevocarToken(string refreshToken, string? ip, string? userAgent);
        Task<CurrentUserInfo?> ObtenerUsuarioActual(Guid userId);
        Task<ResultadoAuth<bool>> CambiarPassword(Guid userId, CambiarPasswordRequest request, string? ip, string? userAgent);
    }
}
