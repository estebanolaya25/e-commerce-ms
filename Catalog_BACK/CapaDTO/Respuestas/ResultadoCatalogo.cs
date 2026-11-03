namespace CapaDto.Respuestas
{
    public enum EstadoResultado
    {
        Exitoso,
        DatosInvalidos,
        NoEncontrado,
        Conflicto
    }

    public class ResultadoCatalogo<T>
    {
        public EstadoResultado Estado { get; private set; }
        public string? Mensaje { get; private set; }
        public T? Datos { get; private set; }
        public bool Exitoso => Estado == EstadoResultado.Exitoso;

        public static ResultadoCatalogo<T> Ok(T datos) => new() { Estado = EstadoResultado.Exitoso, Datos = datos };

        public static ResultadoCatalogo<T> Error(EstadoResultado estado, string mensaje) => new() { Estado = estado, Mensaje = mensaje };
    }
}
