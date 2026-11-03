using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaDatos.Interfaz.marcas.interfaz
{
    public interface IMarcasCapaDatos
    {
        Task<List<MarcaDto>> ListarMarcas(bool incluirInactivas);
        Task<MarcaDto?> ObtenerMarcaPorId(Guid marcaId);
        Task<Guid> CrearMarca(MarcaRequest request, string slug);
        Task<bool> ActualizarMarca(Guid marcaId, MarcaRequest request, string slug);
        Task<bool> CambiarEstadoMarca(Guid marcaId, bool activa);
    }
}
