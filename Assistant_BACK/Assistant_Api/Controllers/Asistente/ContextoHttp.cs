using Assistant_Api.Modelos;
using Microsoft.Net.Http.Headers;

namespace Assistant_Api.Controllers.Asistente
{
    public static class ContextoHttp
    {
        /// <summary>
        /// Usuario de la petición, solo si la autenticación validó el JWT (firma, emisor, vencimiento): un token inválido
        /// se trata como sin sesión. El token se conserva para reenviarlo a los demás servicios.
        /// </summary>
        public static ContextoUsuario? Usuario(HttpContext http)
        {
            if (http.User.Identity?.IsAuthenticated != true)
                return null;

            string cabecera = http.Request.Headers[HeaderNames.Authorization].ToString();
            const string prefijo = "Bearer ";
            if (!cabecera.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                return null;

            List<string> roles = http.User.FindAll("role").Select(c => c.Value).ToList();
            return new ContextoUsuario(cabecera[prefijo.Length..].Trim(), http.User.FindFirst("given_name")?.Value, roles);
        }
    }
}
