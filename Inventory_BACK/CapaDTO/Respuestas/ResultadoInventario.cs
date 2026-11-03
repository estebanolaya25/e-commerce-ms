namespace CapaDto.Respuestas
{
    public enum EstadoResultado
    {
        Exitoso,
        DatosInvalidos,
        NoEncontrado,
        Conflicto,
        StockInsuficiente
    }

    public class ResultadoInventario<T>
    {
        public EstadoResultado Estado { get; private set; }
        public string? Mensaje { get; private set; }
        public T? Datos { get; private set; }
        public List<FaltanteDto>? Faltantes { get; private set; }
        public bool Exitoso => Estado == EstadoResultado.Exitoso;

        public static ResultadoInventario<T> Ok(T datos) => new() { Estado = EstadoResultado.Exitoso, Datos = datos };

        public static ResultadoInventario<T> Error(EstadoResultado estado, string mensaje) => new() { Estado = estado, Mensaje = mensaje };

        public static ResultadoInventario<T> SinStock(List<FaltanteDto> faltantes) => new()
        {
            Estado = EstadoResultado.StockInsuficiente,
            Mensaje = "No hay stock suficiente para uno o más productos.",
            Faltantes = faltantes
        };
    }
}
