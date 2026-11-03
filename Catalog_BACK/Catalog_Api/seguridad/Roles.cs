using System.Security.Claims;

namespace Catalog_Api.seguridad
{
    public static class Roles
    {
        public const string Administrador = "Administrator";
        public const string GestorCatalogo = "CatalogManager";

        // Para [Authorize(Roles = ...)]: la coma significa "cualquiera de los dos".
        public const string GestionCatalogo = Administrador + "," + GestorCatalogo;

        public static bool PuedeGestionarCatalogo(ClaimsPrincipal usuario) =>
            usuario.IsInRole(Administrador) || usuario.IsInRole(GestorCatalogo);
    }
}
