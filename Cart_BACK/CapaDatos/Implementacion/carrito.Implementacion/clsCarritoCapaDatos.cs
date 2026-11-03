using CapaDatos.Interfaz.carrito.interfaz;
using StackExchange.Redis;

namespace CapaDatos.Implementacion.carrito.Implementacion
{
    // Cada carrito es un hash de Redis: ecommerce:carrito:{usuarioId} -> { SKU: cantidad }.
    // Los scripts Lua se ejecutan de forma atómica dentro de Redis: nadie puede modificar el carrito a mitad del script.
    public class clsCarritoCapaDatos : ICarritoCapaDatos
    {
        private const string ScriptSumar = @"
            local actual = tonumber(redis.call('HGET', KEYS[1], ARGV[1]) or '0')
            if actual == 0 and redis.call('HLEN', KEYS[1]) >= tonumber(ARGV[4]) then
                return -1
            end
            local nueva = actual + tonumber(ARGV[2])
            if nueva > tonumber(ARGV[3]) then
                return -2
            end
            redis.call('HSET', KEYS[1], ARGV[1], nueva)
            redis.call('EXPIRE', KEYS[1], ARGV[5])
            return nueva";

        private const string ScriptEstablecer = @"
            if redis.call('HEXISTS', KEYS[1], ARGV[1]) == 0 then
                return 0
            end
            redis.call('HSET', KEYS[1], ARGV[1], ARGV[2])
            redis.call('EXPIRE', KEYS[1], ARGV[3])
            return 1";

        private readonly IConnectionMultiplexer _redis;

        public clsCarritoCapaDatos(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        private IDatabase Db => _redis.GetDatabase();

        private static RedisKey Clave(Guid usuarioId) => $"ecommerce:carrito:{usuarioId:N}";

        public async Task<Dictionary<string, int>> ObtenerItems(Guid usuarioId)
        {
            HashEntry[] entradas = await Db.HashGetAllAsync(Clave(usuarioId));
            return entradas.ToDictionary(e => e.Name.ToString(), e => (int)e.Value);
        }

        public async Task<int> ObtenerCantidad(Guid usuarioId, string sku)
        {
            RedisValue valor = await Db.HashGetAsync(Clave(usuarioId), sku);
            return valor.HasValue ? (int)valor : 0;
        }

        public async Task<int> SumarCantidad(Guid usuarioId, string sku, int cantidad, int maxUnidadesPorProducto, int maxProductos, TimeSpan expiracion)
        {
            RedisResult resultado = await Db.ScriptEvaluateAsync(
                ScriptSumar,
                new[] { Clave(usuarioId) },
                new RedisValue[] { sku, cantidad, maxUnidadesPorProducto, maxProductos, (long)expiracion.TotalSeconds });
            return (int)resultado;
        }

        public async Task<bool> EstablecerCantidad(Guid usuarioId, string sku, int cantidad, TimeSpan expiracion)
        {
            RedisResult resultado = await Db.ScriptEvaluateAsync(
                ScriptEstablecer,
                new[] { Clave(usuarioId) },
                new RedisValue[] { sku, cantidad, (long)expiracion.TotalSeconds });
            return (int)resultado == 1;
        }

        public Task<bool> QuitarItem(Guid usuarioId, string sku) => Db.HashDeleteAsync(Clave(usuarioId), sku);

        public Task<long> QuitarItems(Guid usuarioId, IEnumerable<string> skus) =>
            Db.HashDeleteAsync(Clave(usuarioId), skus.Select(s => (RedisValue)s).ToArray());

        public Task VaciarCarrito(Guid usuarioId) => Db.KeyDeleteAsync(Clave(usuarioId));
    }
}
