-- Opcional. Ejecutar conectado a la base "ecommerce_catalog":
-- psql -U postgres -h localhost -d ecommerce_catalog -f 03_datos_prueba.sql

BEGIN;

INSERT INTO catalogo.categorias (nombre, slug, orden) VALUES
    ('Hombre', 'hombre', 1),
    ('Mujer', 'mujer', 2);

INSERT INTO catalogo.categorias (categoria_padre_id, nombre, slug, orden)
SELECT id, 'Camisas', 'camisas-hombre', 1 FROM catalogo.categorias WHERE slug = 'hombre';

INSERT INTO catalogo.marcas (nombre, slug) VALUES ('Marca Demo', 'marca-demo');

INSERT INTO catalogo.productos (categoria_id, marca_id, nombre, slug, descripcion_corta, descripcion, precio_base, atributos, destacado)
SELECT c.id, m.id,
       'Camisa Oxford',
       'camisa-oxford',
       'Camisa de algodón manga larga',
       'Camisa clásica tipo Oxford en algodón 100%, ideal para oficina.',
       129900,
       '{"material": "algodón", "manga": "larga"}'::jsonb,
       true
FROM catalogo.categorias c, catalogo.marcas m
WHERE c.slug = 'camisas-hombre' AND m.slug = 'marca-demo';

INSERT INTO catalogo.producto_variantes (producto_id, sku, opciones, precio, precio_antes)
SELECT p.id, v.sku, v.opciones::jsonb, v.precio, v.precio_antes
FROM catalogo.productos p
CROSS JOIN (VALUES
    ('OXF-AZ-S', '{"color": "Azul", "talla": "S"}', NULL::numeric, NULL::numeric),
    ('OXF-AZ-M', '{"color": "Azul", "talla": "M"}', NULL, NULL),
    ('OXF-BL-M', '{"color": "Blanco", "talla": "M"}', 109900, 129900)
) AS v (sku, opciones, precio, precio_antes)
WHERE p.slug = 'camisa-oxford';

COMMIT;
