namespace CapaDatos.Interfaz.carrito.interfaz
{
    public static class ResultadoSuma
    {
        public const int LimiteProductos = -1;
        public const int LimiteUnidades = -2;
    }

    public interface ICarritoCapaDatos
    {
        Task<Dictionary<string, int>> ObtenerItems(Guid usuarioId);
        Task<int> ObtenerCantidad(Guid usuarioId, string sku);
        Task<int> SumarCantidad(Guid usuarioId, string sku, int cantidad, int maxUnidadesPorProducto, int maxProductos, TimeSpan expiracion);
        Task<bool> EstablecerCantidad(Guid usuarioId, string sku, int cantidad, TimeSpan expiracion);
        Task<bool> QuitarItem(Guid usuarioId, string sku);
        Task<long> QuitarItems(Guid usuarioId, IEnumerable<string> skus);
        Task VaciarCarrito(Guid usuarioId);
    }
}
