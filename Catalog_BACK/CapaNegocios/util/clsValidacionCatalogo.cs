using CapaDatos.Excepciones;

namespace CapaNegocios.util
{
    public static class clsValidacionCatalogo
    {
        private const int MaxEntradas = 20;
        private const int MaxLargoClave = 50;
        private const int MaxLargoValor = 200;

        public static string? ValidarDiccionario(Dictionary<string, string>? valores, string nombreCampo)
        {
            if (valores == null || valores.Count == 0)
                return null;

            if (valores.Count > MaxEntradas)
                return $"{nombreCampo} admite como máximo {MaxEntradas} entradas.";

            foreach (var (clave, valor) in valores)
            {
                if (string.IsNullOrWhiteSpace(clave) || clave.Length > MaxLargoClave)
                    return $"Cada clave de {nombreCampo} debe tener entre 1 y {MaxLargoClave} caracteres.";

                if (string.IsNullOrWhiteSpace(valor) || valor.Length > MaxLargoValor)
                    return $"Cada valor de {nombreCampo} debe tener entre 1 y {MaxLargoValor} caracteres.";
            }

            return null;
        }

        public static string MensajeConflicto(ConflictoDatosException ex)
        {
            return ex.Restriccion switch
            {
                "uq_categorias_slug" => "Ya existe una categoría con ese slug.",
                "uq_marcas_nombre" => "Ya existe una marca con ese nombre.",
                "uq_marcas_slug" => "Ya existe una marca con ese slug.",
                "uq_productos_slug" => "Ya existe un producto con ese slug.",
                "uq_variantes_sku" => "Ya existe una variante con ese SKU.",
                "uq_variantes_producto_opciones" => "El producto ya tiene una variante con esas mismas opciones.",
                _ => "El registro ya existe."
            };
        }
    }
}
