using CapaDto.Externos;

namespace CapaDatos.Interfaz.servicios.interfaz
{
    public interface ICatalogoCliente
    {
        // Devuelve null si Catálogo no respondió (caído, lento o con el circuito abierto).
        Task<List<VarianteCatalogoDto>?> ObtenerVariantes(IReadOnlyCollection<string> skus);
    }
}
