using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Assistant_Api.Servicios;

/// <summary>
/// Rango de precio pedido en lenguaje natural. Los embeddings no entienden cantidades ("menos de 200 mil" se parece
/// tanto a 150.000 como a 900.000), así que el rango se extrae con reglas y se aplica después de la búsqueda.
/// </summary>
public record FiltroPrecio(decimal? Minimo, decimal? Maximo)
{
    public bool Vacio => Minimo is null && Maximo is null;

    public bool Cumple(decimal precio) => (Minimo is null || precio >= Minimo) && (Maximo is null || precio <= Maximo);

    // Un valor: "$200.000", "200.000", "200 mil", "200k", "200 lucas", "1,5 millones", "1.2 millón".
    private const string Valor = @"\$?\s*(\d{1,3}(?:[.,]\d{3})+|\d+(?:[.,]\d+)?)\s*(mil\b|k\b|lucas?\b|millones\b|millon\b|palos?\b)?";

    // Tras el valor: ni más dígitos (evita que "10" se lea como "1") ni una unidad que no sea dinero ("hasta 10 unidades").
    private const string NoEsDinero = @"(?![\d.,]|\s*(?:unidad|producto|articulo|prenda|dia|hora|minuto|semana|mes|ano|vez|veces|cm|talla|%))";

    private static readonly Regex PatronEntre = new($@"\bentre\s+{Valor}\s+y\s+{Valor}{NoEsDinero}", RegexOptions.Compiled);

    private static readonly Regex PatronMaximo = new(
        $@"\b(?:menos\s+de|hasta|maximo|max|por\s+debajo\s+de|no\s+mas\s+de|menor(?:es)?\s+(?:a|de|que)|inferior(?:es)?\s+a|que\s+no\s+pase\s+de|tope\s+de|(?:mas\s+)?(?:barat|economic)[oa]s?\s+(?:de|que|a))\s+{Valor}{NoEsDinero}",
        RegexOptions.Compiled);

    private static readonly Regex PatronMinimo = new(
        $@"\b(?:mas\s+de|desde|minimo|por\s+encima\s+de|mayor(?:es)?\s+(?:a|de|que)|superior(?:es)?\s+a|mas\s+car[oa]s?\s+(?:de|que|a))\s+{Valor}{NoEsDinero}",
        RegexOptions.Compiled);

    public static FiltroPrecio Extraer(string texto)
    {
        var normalizado = Normalizar(texto);

        var entre = PatronEntre.Match(normalizado);
        if (entre.Success)
        {
            // "entre 100 y 200 mil": la unidad del segundo valor también aplica al primero si este no trae la suya.
            var unidadComun = entre.Groups[4].Value;
            var a = Convertir(entre.Groups[1].Value, entre.Groups[2].Success ? entre.Groups[2].Value : unidadComun);
            var b = Convertir(entre.Groups[3].Value, unidadComun);
            if (a is not null && b is not null)
            {
                return new FiltroPrecio(Math.Min(a.Value, b.Value), Math.Max(a.Value, b.Value));
            }
        }

        var maximo = PatronMaximo.Match(normalizado);
        var minimo = PatronMinimo.Match(normalizado);
        return new FiltroPrecio(
            minimo.Success ? Convertir(minimo.Groups[1].Value, minimo.Groups[2].Value) : null,
            maximo.Success ? Convertir(maximo.Groups[1].Value, maximo.Groups[2].Value) : null);
    }

    private static decimal? Convertir(string numero, string unidad)
    {
        // "200.000" y "200,000" son miles; "1,5" y "1.5" son decimales (solo tienen sentido con "millones").
        var esMiles = Regex.IsMatch(numero, @"^\d{1,3}(?:[.,]\d{3})+$");
        var limpio = esMiles ? numero.Replace(".", "").Replace(",", "") : numero.Replace(',', '.');
        if (!decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
        {
            return null;
        }

        valor *= unidad switch
        {
            "mil" or "k" or "luca" or "lucas" => 1_000m,
            "millones" or "millon" or "palo" or "palos" => 1_000_000m,
            // Sin unidad, un número pequeño en pesos colombianos casi siempre significa miles ("menos de 150").
            _ => valor < 1_000m ? 1_000m : 1m
        };
        return valor;
    }

    private static string Normalizar(string texto)
    {
        var builder = new StringBuilder();
        foreach (var c in texto.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }
}
