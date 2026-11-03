using CapaDatos.Interfaz.stock.interfaz;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.stock.interfaz;
using Microsoft.Extensions.Logging;

namespace CapaNegocios.Implementacion.stock.Implementacion
{
    public class clsStockCapaNegocios : IStockCapaNegocios
    {
        private const int MaxSkusPorConsulta = 100;
        private const string MensajeSkuNoEncontrado = "El SKU no tiene registro de inventario.";

        private readonly IStockCapaDatos _stockCapaDatos;
        private readonly ILogger<clsStockCapaNegocios> _logger;

        public clsStockCapaNegocios(IStockCapaDatos stockCapaDatos, ILogger<clsStockCapaNegocios> logger)
        {
            _stockCapaDatos = stockCapaDatos;
            _logger = logger;
        }

        public async Task<ResultadoInventario<List<DisponibilidadDto>>> ConsultarDisponibilidad(string? skus)
        {
            List<string> lista = (skus ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpperInvariant())
                .Distinct()
                .ToList();

            if (lista.Count == 0)
                return ResultadoInventario<List<DisponibilidadDto>>.Error(EstadoResultado.DatosInvalidos, "Indique al menos un SKU separado por comas.");

            if (lista.Count > MaxSkusPorConsulta)
                return ResultadoInventario<List<DisponibilidadDto>>.Error(EstadoResultado.DatosInvalidos, $"Máximo {MaxSkusPorConsulta} SKU por consulta.");

            if (lista.Any(s => s.Length > 50))
                return ResultadoInventario<List<DisponibilidadDto>>.Error(EstadoResultado.DatosInvalidos, "Un SKU no puede superar 50 caracteres.");

            return ResultadoInventario<List<DisponibilidadDto>>.Ok(await _stockCapaDatos.ObtenerDisponibilidad(lista));
        }

        public Task<StockDto?> ObtenerStock(string sku) => _stockCapaDatos.ObtenerStock(Normalizar(sku));

        public async Task<PaginaDto<StockDto>> ListarStock(FiltroStockRequest filtro)
        {
            var (items, total) = await _stockCapaDatos.ListarStock(filtro);
            return new PaginaDto<StockDto> { Items = items, Pagina = filtro.pagina, Tamano = filtro.tamano, Total = total };
        }

        public async Task<ResultadoInventario<List<MovimientoDto>>> ListarMovimientos(string sku, int limite)
        {
            string skuNormalizado = Normalizar(sku);
            if (await _stockCapaDatos.ObtenerStock(skuNormalizado) == null)
                return ResultadoInventario<List<MovimientoDto>>.Error(EstadoResultado.NoEncontrado, MensajeSkuNoEncontrado);

            return ResultadoInventario<List<MovimientoDto>>.Ok(await _stockCapaDatos.ListarMovimientos(skuNormalizado, Math.Clamp(limite, 1, 200)));
        }

        public async Task<ResultadoInventario<StockDto>> RegistrarSku(RegistrarSkuRequest request, Guid? usuarioId)
        {
            string sku = Normalizar(request.sku);
            if (!await _stockCapaDatos.RegistrarSku(sku, request.productoId, request.varianteId, usuarioId, "Registro manual"))
                return ResultadoInventario<StockDto>.Error(EstadoResultado.Conflicto, "El SKU ya tiene registro de inventario.");

            return ResultadoInventario<StockDto>.Ok((await _stockCapaDatos.ObtenerStock(sku))!);
        }

        public async Task<bool> RegistrarDesdeEvento(Guid mensajeId, string sku, Guid productoId, Guid varianteId)
        {
            bool procesado = await _stockCapaDatos.RegistrarSkuDesdeEvento(mensajeId, "VarianteCreada", Normalizar(sku), productoId, varianteId);

            if (procesado)
                _logger.LogInformation("Stock registrado para el SKU {Sku} desde el evento {MensajeId}.", sku, mensajeId);
            else
                _logger.LogInformation("Evento {MensajeId} ya procesado; se ignora el duplicado.", mensajeId);

            return procesado;
        }

        public async Task<ResultadoInventario<StockDto>> RegistrarEntrada(string sku, MovimientoStockRequest request, Guid? usuarioId)
        {
            if (request.cantidad <= 0)
                return ResultadoInventario<StockDto>.Error(EstadoResultado.DatosInvalidos, "La cantidad de una entrada debe ser mayor que cero.");

            return await AplicarMovimiento(Normalizar(sku), request.cantidad, "Entrada", request.motivo.Trim(), usuarioId);
        }

        public async Task<ResultadoInventario<StockDto>> RegistrarAjuste(string sku, MovimientoStockRequest request, Guid? usuarioId)
        {
            if (request.cantidad == 0)
                return ResultadoInventario<StockDto>.Error(EstadoResultado.DatosInvalidos, "La cantidad de un ajuste no puede ser cero.");

            return await AplicarMovimiento(Normalizar(sku), request.cantidad, "Ajuste", request.motivo.Trim(), usuarioId);
        }

        public async Task<ResultadoInventario<StockDto>> ActualizarStockMinimo(string sku, int stockMinimo)
        {
            string skuNormalizado = Normalizar(sku);
            if (!await _stockCapaDatos.ActualizarStockMinimo(skuNormalizado, stockMinimo))
                return ResultadoInventario<StockDto>.Error(EstadoResultado.NoEncontrado, MensajeSkuNoEncontrado);

            return ResultadoInventario<StockDto>.Ok((await _stockCapaDatos.ObtenerStock(skuNormalizado))!);
        }

        private async Task<ResultadoInventario<StockDto>> AplicarMovimiento(string sku, int cantidad, string tipo, string motivo, Guid? usuarioId)
        {
            if (await _stockCapaDatos.AplicarMovimientoFisico(sku, cantidad, tipo, motivo, usuarioId))
                return ResultadoInventario<StockDto>.Ok((await _stockCapaDatos.ObtenerStock(sku))!);

            StockDto? actual = await _stockCapaDatos.ObtenerStock(sku);
            if (actual == null)
                return ResultadoInventario<StockDto>.Error(EstadoResultado.NoEncontrado, MensajeSkuNoEncontrado);

            return ResultadoInventario<StockDto>.Error(EstadoResultado.DatosInvalidos,
                $"El ajuste dejaría {actual.CantidadFisica + cantidad} unidades físicas, menos que las {actual.CantidadReservada} reservadas para pedidos.");
        }

        private static string Normalizar(string sku) => sku.Trim().ToUpperInvariant();
    }
}
