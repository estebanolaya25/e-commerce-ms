using CapaDto.Modelos;
using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.pedidos.interfaz
{
    public interface IPedidosCapaDatos
    {
        Task<long> ObtenerSiguienteNumero();
        Task<bool> CrearPedido(NuevoPedido pedido, EventoOutbox evento);
        Task<PedidoDto?> ObtenerPedido(Guid pedidoId);
        Task<PedidoDto?> ObtenerPorIdempotencia(Guid usuarioId, string idempotencyKey);
        Task<(List<PedidoResumenDto> Items, int Total)> ListarPedidos(Guid? usuarioId, FiltroPedidosRequest filtro, bool soloDespachables = false);
        Task<ResultadoTransicion> AplicarTransicion(TransicionPedido transicion);
        Task<List<Guid>> ObtenerPedidosVencidos(int limite, TimeSpan graciaPagoEnProceso);
    }
}
