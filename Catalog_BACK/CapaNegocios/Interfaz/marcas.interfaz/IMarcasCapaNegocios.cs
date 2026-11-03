using CapaDto.Peticiones;
using CapaDto.Respuestas;

namespace CapaNegocios.Interfaz.marcas.interfaz
{
    public interface IMarcasCapaNegocios
    {
        Task<List<MarcaDto>> Listar(bool incluirInactivas);
        Task<ResultadoCatalogo<MarcaDto>> Crear(MarcaRequest request);
        Task<ResultadoCatalogo<MarcaDto>> Actualizar(Guid marcaId, MarcaRequest request);
        Task<ResultadoCatalogo<bool>> CambiarEstado(Guid marcaId, bool activa);
    }
}
