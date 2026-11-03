using CapaDto.Externos;
using CapaDto.Peticiones;

namespace CapaDatos.Interfaz.servicios.interfaz
{
    public interface ICarritoCliente
    {
        // Reenvía el token del usuario: Carrito sabe de quién es el carrito por el "sub" del JWT.
        Task<CarritoExternoDto?> ObtenerCarrito(string autorizacionUsuario);
    }

    public interface IInventarioCliente
    {
        Task<ResultadoReservaExterna> Reservar(Guid pedidoId, IEnumerable<(string Sku, int Cantidad)> items);
        Task<EstadoLlamada> Confirmar(Guid pedidoId);
        Task<EstadoLlamada> Liberar(Guid pedidoId);
    }

    public interface IPagosCliente
    {
        Task<ResultadoPagoExterno> IniciarPago(Guid pedidoId, decimal monto, TarjetaRequest tarjeta);
        Task<EstadoLlamada> Reembolsar(Guid pagoId, string motivo);
    }
}
