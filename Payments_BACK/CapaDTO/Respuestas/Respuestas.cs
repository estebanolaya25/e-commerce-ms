namespace CapaDto.Respuestas
{
    public enum EstadoResultado
    {
        Exitoso,
        DatosInvalidos,
        NoEncontrado,
        Conflicto
    }

    public class ResultadoPago<T>
    {
        public EstadoResultado Estado { get; private set; }
        public string? Mensaje { get; private set; }
        public T? Datos { get; private set; }
        public bool YaExistia { get; private set; }
        public bool Exitoso => Estado == EstadoResultado.Exitoso;

        public static ResultadoPago<T> Ok(T datos, bool yaExistia = false) => new() { Estado = EstadoResultado.Exitoso, Datos = datos, YaExistia = yaExistia };

        public static ResultadoPago<T> Error(EstadoResultado estado, string mensaje) => new() { Estado = estado, Mensaje = mensaje };
    }

    public static class EstadoPago
    {
        public const string Procesando = "Procesando";
        public const string Aprobado = "Aprobado";
        public const string Rechazado = "Rechazado";
        public const string Reembolsado = "Reembolsado";
    }

    public class PagoDto
    {
        public Guid PagoId { get; set; }
        public Guid PedidoId { get; set; }
        public decimal Monto { get; set; }
        public string Moneda { get; set; } = "COP";
        public string Estado { get; set; } = string.Empty;
        public string Marca { get; set; } = string.Empty;
        public string Ultimos4 { get; set; } = string.Empty;
        public string Titular { get; set; } = string.Empty;
        public string? Referencia { get; set; }
        public string? MotivoRechazo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcesadoEn { get; set; }
        public DateTime? ReembolsadoEn { get; set; }
    }

    public class PaginaDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Pagina { get; set; }
        public int Tamano { get; set; }
        public int Total { get; set; }
        public int TotalPaginas => Tamano == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamano);
    }
}
