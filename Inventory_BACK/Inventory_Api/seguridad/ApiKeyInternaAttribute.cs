using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;

namespace Inventory_Api.seguridad
{
    // Protege los endpoints que solo deben llamar otros microservicios (por ejemplo Pedidos).
    // El gateway no enruta /internal, y además se exige una clave compartida en la cabecera.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiKeyInternaAttribute : Attribute, IAuthorizationFilter
    {
        public const string Cabecera = "X-Api-Key";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            string? esperada = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Interno:ApiKey"];
            string recibida = context.HttpContext.Request.Headers[Cabecera].ToString();

            if (string.IsNullOrEmpty(esperada) || !SonIguales(esperada, recibida))
                context.Result = new UnauthorizedObjectResult(new { mensaje = "Clave de servicio interno inválida." });
        }

        private static bool SonIguales(string a, string b)
        {
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(a)),
                SHA256.HashData(Encoding.UTF8.GetBytes(b)));
        }
    }
}
