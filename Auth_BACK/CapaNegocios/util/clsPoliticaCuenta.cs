namespace CapaNegocios.util
{
    // Reglas de cuenta compartidas por el registro público y la creación de usuarios desde el panel.
    public static class clsPoliticaCuenta
    {
        public static string NormalizarEmail(string email) => email.Trim().ToUpperInvariant();

        public static string? ValidarPassword(string password)
        {
            if (password.Length < 8)
                return "La contraseña debe tener al menos 8 caracteres.";
            if (!password.Any(char.IsUpper))
                return "La contraseña debe tener al menos una letra mayúscula.";
            if (!password.Any(char.IsLower))
                return "La contraseña debe tener al menos una letra minúscula.";
            if (!password.Any(char.IsDigit))
                return "La contraseña debe tener al menos un número.";

            return null;
        }
    }
}
