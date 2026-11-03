namespace CapaDto.Respuestas
{
    public static class EstadoPedido
    {
        public const string PendientePago = "PendientePago";
        public const string PagoEnProceso = "PagoEnProceso";
        public const string Pagado = "Pagado";
        public const string EnPreparacion = "EnPreparacion";
        public const string Enviado = "Enviado";
        public const string Entregado = "Entregado";
        public const string Cancelado = "Cancelado";
        public const string Expirado = "Expirado";
    }

    public class PedidoDto
    {
        public Guid PedidoId { get; set; }
        public string Numero { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public string? UsuarioEmail { get; set; }
        public string Estado { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal CostoEnvio { get; set; }
        public decimal Total { get; set; }
        public string Moneda { get; set; } = "COP";
        public DireccionDto Direccion { get; set; } = new();
        public Guid? PagoId { get; set; }
        public string? UltimoErrorPago { get; set; }
        public DateTime ExpiraEn { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ItemPedidoDto> Items { get; set; } = new();
        public List<HistorialPedidoDto> Historial { get; set; } = new();
    }

    public class DireccionDto
    {
        public string NombreDestinatario { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string? Notas { get; set; }
    }

    public class ItemPedidoDto
    {
        public string Sku { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Opciones { get; set; }
        public string? ImagenUrl { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int Cantidad { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class HistorialPedidoDto
    {
        public string? EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; } = string.Empty;
        public string? Motivo { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PedidoResumenDto
    {
        public Guid PedidoId { get; set; }
        public string Numero { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int Unidades { get; set; }
        public DateTime ExpiraEn { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaginaDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Pagina { get; set; }
        public int Tamano { get; set; }
        public int Total { get; set; }
        public int TotalPaginas => Tamano == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamano);
    }

    public class PagoIniciadoDto
    {
        public Guid PedidoId { get; set; }
        public Guid PagoId { get; set; }
        public string EstadoPedido { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }
}
