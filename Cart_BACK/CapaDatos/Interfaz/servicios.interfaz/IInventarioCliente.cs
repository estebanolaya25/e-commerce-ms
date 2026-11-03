using CapaDto.Externos;

namespace CapaDatos.Interfaz.servicios.interfaz
{
    public interface IInventarioCliente
    {
        // Devuelve null si Inventario no respondió (caído, lento o con el circuito abierto).
        Task<List<DisponibilidadInventarioDto>?> ObtenerDisponibilidad(IReadOnlyCollection<string> skus);
    }
}
