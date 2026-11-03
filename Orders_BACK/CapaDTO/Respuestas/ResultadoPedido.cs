namespace CapaDto.Respuestas
{
    public enum EstadoResultado
    {
        Exitoso,
        DatosInvalidos,
        NoEncontrado,
        Conflicto,
        StockInsuficiente,
        ServicioNoDisponible
    }

    public class ResultadoPedido<T>
    {
        public EstadoResultado Estado { get; private set; }
        public string? Mensaje { get; private set; }
        public T? Datos { get; private set; }
        public bool YaExistia { get; private set; }
        public List<FaltanteDto>? Faltantes { get; private set; }
        public bool Exitoso => Estado == EstadoResultado.Exitoso;

        public static ResultadoPedido<T> Ok(T datos, bool yaExistia = false) => new() { Estado = EstadoResultado.Exitoso, Datos = datos, YaExistia = yaExistia };

        public static ResultadoPedido<T> Error(EstadoResultado estado, string mensaje) => new() { Estado = estado, Mensaje = mensaje };

        public static ResultadoPedido<T> SinStock(List<FaltanteDto> faltantes) => new()
        {
            Estado = EstadoResultado.StockInsuficiente,
            Mensaje = "No hay stock suficiente para uno o más productos.",
            Faltantes = faltantes
        };
    }

    public class FaltanteDto
    {
        public string Sku { get; set; } = string.Empty;
        public int Solicitado { get; set; }
        public int Disponible { get; set; }
    }
}
