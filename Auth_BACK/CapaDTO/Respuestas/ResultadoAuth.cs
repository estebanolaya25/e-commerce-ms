namespace CapaDto.Respuestas
{
    public enum EstadoResultado
    {
        Exitoso,
        DatosInvalidos,
        CredencialesInvalidas,
        CuentaBloqueada,
        CuentaInactiva,
        EmailRegistrado,
        TokenInvalido,
        NoEncontrado,
        OperacionNoPermitida
    }

    public class ResultadoAuth<T>
    {
        public EstadoResultado Estado { get; private set; }
        public string? Mensaje { get; private set; }
        public T? Datos { get; private set; }
        public bool Exitoso => Estado == EstadoResultado.Exitoso;

        public static ResultadoAuth<T> Ok(T datos) => new() { Estado = EstadoResultado.Exitoso, Datos = datos };

        public static ResultadoAuth<T> Error(EstadoResultado estado, string mensaje) => new() { Estado = estado, Mensaje = mensaje };
    }
}
