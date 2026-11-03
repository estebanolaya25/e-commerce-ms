using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.auth.interfaz
{
    public interface ITokenGenerator
    {
        AccessTokenDto GenerarAccessToken(Guid userId, string email, string nombre, string apellido, IEnumerable<string> roles);
        string GenerarRefreshToken();
        DateTime ObtenerExpiracionRefreshToken();
    }
}
