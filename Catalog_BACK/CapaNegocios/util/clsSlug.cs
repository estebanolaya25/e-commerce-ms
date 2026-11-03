using System.Globalization;
using System.Text;

namespace CapaNegocios.util
{
    public static class clsSlug
    {
        public static string Generar(string texto)
        {
            string normalizado = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var resultado = new StringBuilder(normalizado.Length);
            bool ultimoFueGuion = true;

            foreach (char c in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    resultado.Append(c);
                    ultimoFueGuion = false;
                }
                else if (!ultimoFueGuion)
                {
                    resultado.Append('-');
                    ultimoFueGuion = true;
                }
            }

            return resultado.ToString().TrimEnd('-');
        }
    }
}
