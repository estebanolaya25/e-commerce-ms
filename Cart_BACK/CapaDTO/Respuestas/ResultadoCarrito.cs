namespace CapaDto.Respuestas
{
    public enum EstadoResultado
    {
        Exitoso,
        DatosInvalidos,
        NoEncontrado,
        Conflicto,
        ServicioNoDisponible
    }

    public class ResultadoCarrito<T>
    {
        public EstadoResultado Estado { get; private set; }
        public string? Mensaje { get; private set; }
        public T? Datos { get; private set; }
        public bool Exitoso => Estado == EstadoResultado.Exitoso;

        public static ResultadoCarrito<T> Ok(T datos) => new() { Estado = EstadoResultado.Exitoso, Datos = datos };

        public static ResultadoCarrito<T> Error(EstadoResultado estado, string mensaje) => new() { Estado = estado, Mensaje = mensaje };
    }
}
