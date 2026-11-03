using CapaDatos.Interfaz.productos.interfaz;
using CapaDatos.util;
using CapaDto.Peticiones;
using CapaDto.Respuestas;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using System.Data.Common;

namespace CapaDatos.Implementacion.productos.Implementacion
{
    public class clsProductosCapaDatos : IProductosCapaDatos
    {
        private readonly IConfiguration _configuration;

        public clsProductosCapaDatos(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private NpgsqlConnection NuevaConexion() => new NpgsqlConnection(_configuration.GetConnectionString("PostgresConnectionString"));

        public async Task<(List<ProductoResumenDto> Items, int Total)> ListarProductos(FiltroProductosRequest filtro, bool incluirInactivos)
        {
            const string query = @"
                WITH RECURSIVE arbol AS (
                    SELECT id FROM catalogo.categorias WHERE slug = @categoria
                    UNION
                    SELECT c.id
                    FROM catalogo.categorias c
                    INNER JOIN arbol a ON c.categoria_padre_id = a.id
                    WHERE @incluir_inactivos OR c.activa
                )
                SELECT p.id, p.nombre, p.slug, p.descripcion_corta, p.destacado, p.activo,
                       c.nombre AS categoria, c.slug AS categoria_slug,
                       m.nombre AS marca, m.slug AS marca_slug,
                       pr.precio_desde, pr.precio_antes,
                       img.ruta_archivo, img.texto_alternativo,
                       COUNT(*) OVER () AS total
                FROM catalogo.productos p
                INNER JOIN catalogo.categorias c ON c.id = p.categoria_id
                LEFT JOIN catalogo.marcas m ON m.id = p.marca_id
                INNER JOIN LATERAL (
                    SELECT COALESCE(v.precio, p.precio_base) AS precio_desde,
                           v.precio_antes
                    FROM catalogo.producto_variantes v
                    WHERE v.producto_id = p.id AND (@incluir_inactivos OR v.activa)
                    ORDER BY COALESCE(v.precio, p.precio_base) ASC, v.precio_antes DESC NULLS LAST, v.id
                    LIMIT 1
                ) pr ON pr.precio_desde IS NOT NULL
                LEFT JOIN LATERAL (
                    SELECT i.ruta_archivo, i.texto_alternativo
                    FROM catalogo.producto_imagenes i
                    WHERE i.producto_id = p.id
                    ORDER BY i.es_principal DESC, i.orden, i.id
                    LIMIT 1
                ) img ON true
                WHERE (@incluir_inactivos OR (p.activo AND c.activa AND (m.id IS NULL OR m.activa)))
                  AND (@categoria IS NULL OR p.categoria_id IN (SELECT id FROM arbol))
                  AND (@marca IS NULL OR m.slug = @marca)
                  AND (@buscar IS NULL OR p.busqueda @@ plainto_tsquery('spanish', @buscar))
                  AND (@precio_min IS NULL OR pr.precio_desde >= @precio_min)
                  AND (@precio_max IS NULL OR pr.precio_desde <= @precio_max)
                  AND (NOT @solo_destacados OR p.destacado)
                ORDER BY
                    CASE WHEN @orden = 'precio_asc' THEN pr.precio_desde END ASC,
                    CASE WHEN @orden = 'precio_desc' THEN pr.precio_desde END DESC,
                    CASE WHEN @orden = 'nombre' THEN p.nombre END ASC,
                    CASE WHEN @orden = 'relevancia' AND @buscar IS NOT NULL
                         THEN ts_rank(p.busqueda, plainto_tsquery('spanish', @buscar)) END DESC,
                    p.id DESC
                LIMIT @limite OFFSET @desplazamiento";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "incluir_inactivos", NpgsqlDbType.Boolean, incluirInactivos);
            clsPostgresUtil.Agregar(cmd, "categoria", NpgsqlDbType.Varchar, filtro.categoria);
            clsPostgresUtil.Agregar(cmd, "marca", NpgsqlDbType.Varchar, filtro.marca);
            clsPostgresUtil.Agregar(cmd, "buscar", NpgsqlDbType.Text, filtro.buscar);
            clsPostgresUtil.Agregar(cmd, "precio_min", NpgsqlDbType.Numeric, filtro.precioMin);
            clsPostgresUtil.Agregar(cmd, "precio_max", NpgsqlDbType.Numeric, filtro.precioMax);
            clsPostgresUtil.Agregar(cmd, "solo_destacados", NpgsqlDbType.Boolean, filtro.soloDestacados);
            clsPostgresUtil.Agregar(cmd, "orden", NpgsqlDbType.Text, filtro.orden);
            clsPostgresUtil.Agregar(cmd, "limite", NpgsqlDbType.Integer, filtro.tamano);
            clsPostgresUtil.Agregar(cmd, "desplazamiento", NpgsqlDbType.Integer, (filtro.pagina - 1) * filtro.tamano);

            var productos = new List<ProductoResumenDto>();
            int total = 0;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                total = (int)reader.GetInt64(reader.GetOrdinal("total"));
                productos.Add(new ProductoResumenDto
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    DescripcionCorta = clsPostgresUtil.LeerTexto(reader, "descripcion_corta"),
                    Destacado = reader.GetBoolean(reader.GetOrdinal("destacado")),
                    Activo = reader.GetBoolean(reader.GetOrdinal("activo")),
                    Categoria = reader.GetString(reader.GetOrdinal("categoria")),
                    CategoriaSlug = reader.GetString(reader.GetOrdinal("categoria_slug")),
                    Marca = clsPostgresUtil.LeerTexto(reader, "marca"),
                    MarcaSlug = clsPostgresUtil.LeerTexto(reader, "marca_slug"),
                    PrecioDesde = reader.GetDecimal(reader.GetOrdinal("precio_desde")),
                    PrecioAntes = clsPostgresUtil.LeerDecimal(reader, "precio_antes"),
                    ImagenRuta = clsPostgresUtil.LeerTexto(reader, "ruta_archivo"),
                    ImagenTextoAlternativo = clsPostgresUtil.LeerTexto(reader, "texto_alternativo")
                });
            }
            return (productos, total);
        }

        public Task<ProductoDetalleDto?> ObtenerProductoPorSlug(string slug, bool incluirInactivos)
        {
            return ObtenerProducto(null, slug, incluirInactivos);
        }

        public Task<ProductoDetalleDto?> ObtenerProductoPorId(Guid productoId, bool incluirInactivos)
        {
            return ObtenerProducto(productoId, null, incluirInactivos);
        }

        private async Task<ProductoDetalleDto?> ObtenerProducto(Guid? productoId, string? slug, bool incluirInactivos)
        {
            const string queryProducto = @"
                SELECT p.id, p.nombre, p.slug, p.descripcion_corta, p.descripcion, p.precio_base,
                       p.atributos::text AS atributos, p.destacado, p.activo,
                       c.id AS categoria_id, c.nombre AS categoria, c.slug AS categoria_slug,
                       m.id AS marca_id, m.nombre AS marca, m.slug AS marca_slug
                FROM catalogo.productos p
                INNER JOIN catalogo.categorias c ON c.id = p.categoria_id
                LEFT JOIN catalogo.marcas m ON m.id = p.marca_id
                WHERE (@id IS NULL OR p.id = @id)
                  AND (@slug IS NULL OR p.slug = @slug)
                  AND (@incluir_inactivos OR (p.activo AND c.activa AND (m.id IS NULL OR m.activa)))";

            const string queryVariantes = @"
                SELECT v.id, v.producto_id, v.sku, v.opciones::text AS opciones, v.precio AS precio_propio,
                       COALESCE(v.precio, p.precio_base) AS precio, v.precio_antes, v.activa
                FROM catalogo.producto_variantes v
                INNER JOIN catalogo.productos p ON p.id = v.producto_id
                WHERE v.producto_id = @producto_id AND (@incluir_inactivos OR v.activa)
                ORDER BY v.id";

            const string queryImagenes = @"
                SELECT id, producto_id, variante_id, ruta_archivo, texto_alternativo, orden, es_principal
                FROM catalogo.producto_imagenes
                WHERE producto_id = @producto_id
                ORDER BY es_principal DESC, orden, id";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();

            ProductoDetalleDto producto;
            await using (var cmd = new NpgsqlCommand(queryProducto, connection))
            {
                clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, productoId);
                clsPostgresUtil.Agregar(cmd, "slug", NpgsqlDbType.Varchar, slug);
                clsPostgresUtil.Agregar(cmd, "incluir_inactivos", NpgsqlDbType.Boolean, incluirInactivos);
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return null;

                producto = new ProductoDetalleDto
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    DescripcionCorta = clsPostgresUtil.LeerTexto(reader, "descripcion_corta"),
                    Descripcion = clsPostgresUtil.LeerTexto(reader, "descripcion"),
                    PrecioBase = reader.GetDecimal(reader.GetOrdinal("precio_base")),
                    Atributos = clsPostgresUtil.LeerDiccionario(reader, "atributos"),
                    Destacado = reader.GetBoolean(reader.GetOrdinal("destacado")),
                    Activo = reader.GetBoolean(reader.GetOrdinal("activo")),
                    CategoriaId = reader.GetGuid(reader.GetOrdinal("categoria_id")),
                    Categoria = reader.GetString(reader.GetOrdinal("categoria")),
                    CategoriaSlug = reader.GetString(reader.GetOrdinal("categoria_slug")),
                    MarcaId = clsPostgresUtil.LeerGuid(reader, "marca_id"),
                    Marca = clsPostgresUtil.LeerTexto(reader, "marca"),
                    MarcaSlug = clsPostgresUtil.LeerTexto(reader, "marca_slug")
                };
            }

            await using (var cmd = new NpgsqlCommand(queryVariantes, connection))
            {
                clsPostgresUtil.Agregar(cmd, "producto_id", NpgsqlDbType.Uuid, producto.Id);
                clsPostgresUtil.Agregar(cmd, "incluir_inactivos", NpgsqlDbType.Boolean, incluirInactivos);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    producto.Variantes.Add(MapearVariante(reader));
            }

            await using (var cmd = new NpgsqlCommand(queryImagenes, connection))
            {
                clsPostgresUtil.Agregar(cmd, "producto_id", NpgsqlDbType.Uuid, producto.Id);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    producto.Imagenes.Add(new ImagenDto
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("id")),
                        ProductoId = reader.GetGuid(reader.GetOrdinal("producto_id")),
                        VarianteId = clsPostgresUtil.LeerGuid(reader, "variante_id"),
                        RutaArchivo = reader.GetString(reader.GetOrdinal("ruta_archivo")),
                        TextoAlternativo = clsPostgresUtil.LeerTexto(reader, "texto_alternativo"),
                        Orden = reader.GetInt32(reader.GetOrdinal("orden")),
                        EsPrincipal = reader.GetBoolean(reader.GetOrdinal("es_principal"))
                    });
                }
            }

            return producto;
        }

        public Task<Guid> CrearProducto(CrearProductoRequest request, string slug, Guid? usuarioId)
        {
            const string queryProducto = @"
                INSERT INTO catalogo.productos
                    (categoria_id, marca_id, nombre, slug, descripcion_corta, descripcion,
                     precio_base, atributos, destacado, creado_por, actualizado_por)
                VALUES
                    (@categoria_id, @marca_id, @nombre, @slug, @descripcion_corta, @descripcion,
                     @precio_base, @atributos, @destacado, @usuario_id, @usuario_id)
                RETURNING id";

            return clsPostgresUtil.TraducirConflictos(async () =>
            {
                await using var connection = NuevaConexion();
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();

                Guid productoId;
                await using (var cmd = new NpgsqlCommand(queryProducto, connection, transaction))
                {
                    AgregarParametrosProducto(cmd, request, slug, usuarioId);
                    productoId = (Guid)(await cmd.ExecuteScalarAsync())!;
                }

                foreach (VarianteRequest variante in request.variantes)
                    await InsertarVariante(connection, transaction, productoId, variante);

                await transaction.CommitAsync();
                return productoId;
            });
        }

        public Task<bool> ActualizarProducto(Guid productoId, ProductoRequest request, string slug, Guid? usuarioId)
        {
            const string query = @"
                UPDATE catalogo.productos
                SET categoria_id = @categoria_id, marca_id = @marca_id, nombre = @nombre, slug = @slug,
                    descripcion_corta = @descripcion_corta, descripcion = @descripcion,
                    precio_base = @precio_base, atributos = @atributos, destacado = @destacado,
                    actualizado_por = @usuario_id
                WHERE id = @id";

            return clsPostgresUtil.TraducirConflictos(async () =>
            {
                await using var connection = NuevaConexion();
                await connection.OpenAsync();
                await using var cmd = new NpgsqlCommand(query, connection);
                AgregarParametrosProducto(cmd, request, slug, usuarioId);
                clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, productoId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            });
        }

        public async Task<bool> CambiarEstadoProducto(Guid productoId, bool activo, Guid? usuarioId)
        {
            const string query = "UPDATE catalogo.productos SET activo = @activo, actualizado_por = @usuario_id WHERE id = @id";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "activo", NpgsqlDbType.Boolean, activo);
            clsPostgresUtil.Agregar(cmd, "usuario_id", NpgsqlDbType.Uuid, usuarioId);
            clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, productoId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<VarianteDto?> ObtenerVariante(Guid varianteId)
        {
            const string query = @"
                SELECT v.id, v.producto_id, v.sku, v.opciones::text AS opciones, v.precio AS precio_propio,
                       COALESCE(v.precio, p.precio_base) AS precio, v.precio_antes, v.activa
                FROM catalogo.producto_variantes v
                INNER JOIN catalogo.productos p ON p.id = v.producto_id
                WHERE v.id = @id";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, varianteId);
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapearVariante(reader) : null;
        }

        public async Task<List<VarianteResumenDto>> ObtenerVariantesPorSku(IReadOnlyList<string> skus)
        {
            const string query = @"
                SELECT v.id, v.producto_id, v.sku, v.opciones::text AS opciones,
                       COALESCE(v.precio, p.precio_base) AS precio, v.precio_antes,
                       p.nombre, p.slug,
                       (v.activa AND p.activo AND c.activa AND (m.id IS NULL OR m.activa)) AS disponible,
                       img.ruta_archivo
                FROM catalogo.producto_variantes v
                INNER JOIN catalogo.productos p ON p.id = v.producto_id
                INNER JOIN catalogo.categorias c ON c.id = p.categoria_id
                LEFT JOIN catalogo.marcas m ON m.id = p.marca_id
                LEFT JOIN LATERAL (
                    SELECT i.ruta_archivo
                    FROM catalogo.producto_imagenes i
                    WHERE i.producto_id = p.id
                    ORDER BY (i.variante_id = v.id) DESC NULLS LAST, i.es_principal DESC, i.orden
                    LIMIT 1
                ) img ON true
                WHERE v.sku = ANY(@skus)";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "skus", NpgsqlDbType.Array | NpgsqlDbType.Varchar, skus.ToArray());

            var variantes = new List<VarianteResumenDto>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                variantes.Add(new VarianteResumenDto
                {
                    VarianteId = reader.GetGuid(reader.GetOrdinal("id")),
                    ProductoId = reader.GetGuid(reader.GetOrdinal("producto_id")),
                    Sku = reader.GetString(reader.GetOrdinal("sku")),
                    Opciones = clsPostgresUtil.LeerDiccionario(reader, "opciones"),
                    Precio = reader.GetDecimal(reader.GetOrdinal("precio")),
                    PrecioAntes = clsPostgresUtil.LeerDecimal(reader, "precio_antes"),
                    Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                    Slug = reader.GetString(reader.GetOrdinal("slug")),
                    Disponible = reader.GetBoolean(reader.GetOrdinal("disponible")),
                    ImagenRuta = clsPostgresUtil.LeerTexto(reader, "ruta_archivo")
                });
            }
            return variantes;
        }

        public Task<Guid> CrearVariante(Guid productoId, VarianteRequest request)
        {
            return clsPostgresUtil.TraducirConflictos(async () =>
            {
                await using var connection = NuevaConexion();
                await connection.OpenAsync();
                return await InsertarVariante(connection, null, productoId, request);
            });
        }

        public Task<bool> ActualizarVariante(Guid varianteId, VarianteRequest request)
        {
            const string query = @"
                UPDATE catalogo.producto_variantes
                SET sku = @sku, opciones = @opciones, precio = @precio, precio_antes = @precio_antes
                WHERE id = @id";

            return clsPostgresUtil.TraducirConflictos(async () =>
            {
                await using var connection = NuevaConexion();
                await connection.OpenAsync();
                await using var cmd = new NpgsqlCommand(query, connection);
                AgregarParametrosVariante(cmd, request);
                clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, varianteId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            });
        }

        public async Task<bool> CambiarEstadoVariante(Guid varianteId, bool activa)
        {
            const string query = "UPDATE catalogo.producto_variantes SET activa = @activa WHERE id = @id";

            await using var connection = NuevaConexion();
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(query, connection);
            clsPostgresUtil.Agregar(cmd, "activa", NpgsqlDbType.Boolean, activa);
            clsPostgresUtil.Agregar(cmd, "id", NpgsqlDbType.Uuid, varianteId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static async Task<Guid> InsertarVariante(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid productoId, VarianteRequest request)
        {
            const string query = @"
                INSERT INTO catalogo.producto_variantes (producto_id, sku, opciones, precio, precio_antes)
                VALUES (@producto_id, @sku, @opciones, @precio, @precio_antes)
                RETURNING id";

            await using var cmd = new NpgsqlCommand(query, connection, transaction);
            clsPostgresUtil.Agregar(cmd, "producto_id", NpgsqlDbType.Uuid, productoId);
            AgregarParametrosVariante(cmd, request);
            return (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        private static void AgregarParametrosProducto(NpgsqlCommand cmd, ProductoRequest request, string slug, Guid? usuarioId)
        {
            clsPostgresUtil.Agregar(cmd, "categoria_id", NpgsqlDbType.Uuid, request.categoriaId);
            clsPostgresUtil.Agregar(cmd, "marca_id", NpgsqlDbType.Uuid, request.marcaId);
            clsPostgresUtil.Agregar(cmd, "nombre", NpgsqlDbType.Varchar, request.nombre.Trim());
            clsPostgresUtil.Agregar(cmd, "slug", NpgsqlDbType.Varchar, slug);
            clsPostgresUtil.Agregar(cmd, "descripcion_corta", NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(request.descripcionCorta) ? null : request.descripcionCorta.Trim());
            clsPostgresUtil.Agregar(cmd, "descripcion", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(request.descripcion) ? null : request.descripcion.Trim());
            clsPostgresUtil.Agregar(cmd, "precio_base", NpgsqlDbType.Numeric, request.precioBase);
            clsPostgresUtil.Agregar(cmd, "atributos", NpgsqlDbType.Jsonb, clsPostgresUtil.ADiccionarioJson(request.atributos));
            clsPostgresUtil.Agregar(cmd, "destacado", NpgsqlDbType.Boolean, request.destacado);
            clsPostgresUtil.Agregar(cmd, "usuario_id", NpgsqlDbType.Uuid, usuarioId);
        }

        private static void AgregarParametrosVariante(NpgsqlCommand cmd, VarianteRequest request)
        {
            clsPostgresUtil.Agregar(cmd, "sku", NpgsqlDbType.Varchar, request.sku.Trim().ToUpperInvariant());
            clsPostgresUtil.Agregar(cmd, "opciones", NpgsqlDbType.Jsonb, clsPostgresUtil.ADiccionarioJson(request.opciones));
            clsPostgresUtil.Agregar(cmd, "precio", NpgsqlDbType.Numeric, request.precio);
            clsPostgresUtil.Agregar(cmd, "precio_antes", NpgsqlDbType.Numeric, request.precioAntes);
        }

        private static VarianteDto MapearVariante(DbDataReader reader)
        {
            return new VarianteDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                ProductoId = reader.GetGuid(reader.GetOrdinal("producto_id")),
                Sku = reader.GetString(reader.GetOrdinal("sku")),
                Opciones = clsPostgresUtil.LeerDiccionario(reader, "opciones"),
                PrecioPropio = clsPostgresUtil.LeerDecimal(reader, "precio_propio"),
                Precio = reader.GetDecimal(reader.GetOrdinal("precio")),
                PrecioAntes = clsPostgresUtil.LeerDecimal(reader, "precio_antes"),
                Activa = reader.GetBoolean(reader.GetOrdinal("activa"))
            };
        }
    }
}
