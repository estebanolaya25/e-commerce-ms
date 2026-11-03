using CapaDatos.Interfaz.pagos.interfaz;
using CapaDto.Modelos;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using CapaNegocios.Interfaz.pagos.interfaz;
using Ecommerce.Contratos.Pagos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace CapaNegocios.Implementacion.pagos.Implementacion
{
    // Simula una pasarela de pagos: acepta la solicitud al instante y decide el resultado unos segundos después,
    // como hacen las pasarelas reales (el resultado llega por evento, no en la misma respuesta).
    public class clsPagosCapaNegocios : IPagosCapaNegocios
    {
        private const string TarjetaSiempreAprobada = "4111111111111111";
        private const string TarjetaSiempreRechazada = "4000000000000002";
        private const string CaracteresReferencia = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private static readonly string[] MotivosRechazo =
        {
            "Fondos insuficientes",
            "Transacción rechazada por el banco emisor",
            "Tarjeta bloqueada por seguridad",
            "Excede el cupo disponible"
        };

        private readonly IPagosCapaDatos _pagosCapaDatos;
        private readonly IConfiguration _configuration;
        private readonly ILogger<clsPagosCapaNegocios> _logger;

        public clsPagosCapaNegocios(IPagosCapaDatos pagosCapaDatos, IConfiguration configuration, ILogger<clsPagosCapaNegocios> logger)
        {
            _pagosCapaDatos = pagosCapaDatos;
            _configuration = configuration;
            _logger = logger;
        }

        private double TasaAprobacion => double.TryParse(_configuration["Simulador:TasaAprobacion"], NumberStyles.Float, CultureInfo.InvariantCulture, out double valor) ? valor : 0.8;
        private int SegundosMinimos => int.TryParse(_configuration["Simulador:SegundosMinimos"], out int valor) ? valor : 2;
        private int SegundosMaximos => int.TryParse(_configuration["Simulador:SegundosMaximos"], out int valor) ? valor : 8;

        public async Task<ResultadoPago<PagoDto>> IniciarPago(IniciarPagoRequest request)
        {
            string numero = new(request.numeroTarjeta.Where(char.IsDigit).ToArray());
            if (numero.Length is < 13 or > 19 || !CumpleLuhn(numero))
                return ResultadoPago<PagoDto>.Error(EstadoResultado.DatosInvalidos, "El número de tarjeta no es válido.");

            DateTime ahora = DateTime.UtcNow;
            if (request.anioVencimiento < ahora.Year || (request.anioVencimiento == ahora.Year && request.mesVencimiento < ahora.Month))
                return ResultadoPago<PagoDto>.Error(EstadoResultado.DatosInvalidos, "La tarjeta está vencida.");

            // Si el pedido ya tiene un pago en curso o aprobado, se devuelve ese mismo (idempotencia).
            PagoDto? vigente = await _pagosCapaDatos.ObtenerVigentePorPedido(request.pedidoId);
            if (vigente != null)
                return ResultadoPago<PagoDto>.Ok(vigente, yaExistia: true);

            var nuevo = new NuevoPago
            {
                PedidoId = request.pedidoId,
                Monto = request.monto,
                Marca = DetectarMarca(numero),
                Ultimos4 = numero[^4..],
                Titular = request.titular.Trim(),
                Modo = numero switch
                {
                    TarjetaSiempreAprobada => ModoSimulacion.Aprobar,
                    TarjetaSiempreRechazada => ModoSimulacion.Rechazar,
                    _ => ModoSimulacion.Aleatorio
                },
                ProcesarEn = ahora.AddSeconds(Random.Shared.Next(SegundosMinimos, SegundosMaximos + 1))
            };

            PagoDto? creado = await _pagosCapaDatos.CrearPago(nuevo);
            if (creado == null)
                return ResultadoPago<PagoDto>.Ok((await _pagosCapaDatos.ObtenerVigentePorPedido(request.pedidoId))!, yaExistia: true);

            _logger.LogInformation("Pago {PagoId} del pedido {PedidoId} recibido ({Marca} ****{Ultimos4}, modo {Modo}); se resolverá a las {ProcesarEn:HH:mm:ss}.",
                creado.PagoId, creado.PedidoId, creado.Marca, creado.Ultimos4, nuevo.Modo, nuevo.ProcesarEn);
            return ResultadoPago<PagoDto>.Ok(creado);
        }

        public Task<PagoDto?> ObtenerPago(Guid pagoId) => _pagosCapaDatos.ObtenerPago(pagoId);

        public async Task<PaginaDto<PagoDto>> ListarPagos(FiltroPagosRequest filtro)
        {
            var (items, total) = await _pagosCapaDatos.ListarPagos(filtro);
            return new PaginaDto<PagoDto> { Items = items, Pagina = filtro.pagina, Tamano = filtro.tamano, Total = total };
        }

        public async Task<ResultadoPago<PagoDto>> Reembolsar(Guid pagoId, string motivo)
        {
            PagoDto? pago = await _pagosCapaDatos.ObtenerPago(pagoId);
            if (pago == null)
                return ResultadoPago<PagoDto>.Error(EstadoResultado.NoEncontrado, "Pago no encontrado.");
            if (pago.Estado == EstadoPago.Reembolsado)
                return ResultadoPago<PagoDto>.Ok(pago, yaExistia: true);
            if (pago.Estado != EstadoPago.Aprobado)
                return ResultadoPago<PagoDto>.Error(EstadoResultado.Conflicto, $"Solo se reembolsan pagos aprobados; este está {pago.Estado}.");

            var evento = EventoOutbox.Crear(new PagoReembolsado
            {
                PagoId = pago.PagoId,
                PedidoId = pago.PedidoId,
                Monto = pago.Monto,
                Motivo = motivo,
                ReembolsadoEn = DateTime.UtcNow
            });

            await _pagosCapaDatos.Reembolsar(pagoId, evento);
            _logger.LogInformation("Pago {PagoId} reembolsado: {Motivo}", pagoId, motivo);
            return ResultadoPago<PagoDto>.Ok((await _pagosCapaDatos.ObtenerPago(pagoId))!);
        }

        public async Task<int> ProcesarPendientes()
        {
            int procesados = 0;
            foreach (PagoPorProcesar pago in await _pagosCapaDatos.ObtenerPorProcesar(20))
            {
                bool aprobado = pago.Modo switch
                {
                    ModoSimulacion.Aprobar => true,
                    ModoSimulacion.Rechazar => false,
                    _ => Random.Shared.NextDouble() < TasaAprobacion
                };

                bool resuelto;
                if (aprobado)
                {
                    string referencia = "SIM-" + new string(Enumerable.Range(0, 10).Select(_ => CaracteresReferencia[Random.Shared.Next(CaracteresReferencia.Length)]).ToArray());
                    resuelto = await _pagosCapaDatos.ResolverPago(pago.PagoId, true, referencia, null, EventoOutbox.Crear(new PagoAprobado
                    {
                        PagoId = pago.PagoId,
                        PedidoId = pago.PedidoId,
                        Monto = pago.Monto,
                        Referencia = referencia,
                        AprobadoEn = DateTime.UtcNow
                    }));
                }
                else
                {
                    string motivo = pago.Modo == ModoSimulacion.Rechazar ? MotivosRechazo[0] : MotivosRechazo[Random.Shared.Next(MotivosRechazo.Length)];
                    resuelto = await _pagosCapaDatos.ResolverPago(pago.PagoId, false, null, motivo, EventoOutbox.Crear(new PagoRechazado
                    {
                        PagoId = pago.PagoId,
                        PedidoId = pago.PedidoId,
                        Motivo = motivo,
                        RechazadoEn = DateTime.UtcNow
                    }));
                }

                if (resuelto)
                {
                    procesados++;
                    _logger.LogInformation("Pago {PagoId} {Resultado}.", pago.PagoId, aprobado ? "APROBADO" : "RECHAZADO");
                }
            }
            return procesados;
        }

        private static bool CumpleLuhn(string numero)
        {
            int suma = 0;
            bool duplicar = false;
            for (int i = numero.Length - 1; i >= 0; i--)
            {
                int digito = numero[i] - '0';
                if (duplicar)
                {
                    digito *= 2;
                    if (digito > 9)
                        digito -= 9;
                }
                suma += digito;
                duplicar = !duplicar;
            }
            return suma % 10 == 0;
        }

        private static string DetectarMarca(string numero)
        {
            if (numero.StartsWith('4'))
                return "Visa";
            if (numero.Length >= 2 && (int.Parse(numero[..2]) is >= 51 and <= 55 || (numero.Length >= 4 && int.Parse(numero[..4]) is >= 2221 and <= 2720)))
                return "Mastercard";
            if (numero.StartsWith("34") || numero.StartsWith("37"))
                return "Amex";
            return "Otra";
        }
    }
}
