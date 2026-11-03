using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.carrito.interfaz
{
    public interface ICarritoCapaNegocios
    {
        Task<CarritoDto> Obtener(Guid usuarioId);
        Task<ResultadoCarrito<CarritoDto>> Agregar(Guid usuarioId, AgregarItemRequest request);
        Task<ResultadoCarrito<CarritoDto>> ActualizarCantidad(Guid usuarioId, string sku, int cantidad);
        Task<ResultadoCarrito<CarritoDto>> Quitar(Guid usuarioId, string sku);
        Task Vaciar(Guid usuarioId);
        Task<long> QuitarComprados(Guid usuarioId, IEnumerable<string> skus);
    }
}
