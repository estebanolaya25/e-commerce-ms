namespace CapaDto.Respuestas
{
    public static class EstadoItem
    {
        public const string Disponible = "Disponible";
        public const string PocasUnidades = "PocasUnidades";
        public const string StockInsuficiente = "StockInsuficiente";
        public const string Agotado = "Agotado";
        public const string NoDisponible = "NoDisponible";
        public const string SinInformacion = "SinInformacion";
    }

    public class CarritoDto
    {
        public List<ItemCarritoDto> Items { get; set; } = new();
        public int ProductosDistintos { get; set; }
        public int Unidades { get; set; }
        public decimal Total { get; set; }
        public bool PuedeComprar { get; set; }
        public bool InformacionCompleta { get; set; } = true;
        public List<string> Avisos { get; set; } = new();
    }

    public class ItemCarritoDto
    {
        public string Sku { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public string? Nombre { get; set; }
        public string? Slug { get; set; }
        public Dictionary<string, string> Opciones { get; set; } = new();
        public string? ImagenUrl { get; set; }
        public decimal? PrecioUnitario { get; set; }
        public decimal? PrecioAntes { get; set; }
        public decimal? Subtotal { get; set; }
        public int? StockDisponible { get; set; }
        public string Estado { get; set; } = EstadoItem.SinInformacion;
    }
}
