-- Ejecutar conectado a la base "ecommerce_catalog" con un superusuario:
-- psql -U postgres -h localhost -d ecommerce_catalog -f 02_permisos.sql

REVOKE ALL ON SCHEMA public FROM PUBLIC;

GRANT USAGE ON SCHEMA catalogo TO catalog_app;

GRANT SELECT, INSERT, UPDATE ON
    catalogo.categorias,
    catalogo.marcas,
    catalogo.productos,
    catalogo.producto_variantes
TO catalog_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON catalogo.producto_imagenes TO catalog_app;

ALTER ROLE catalog_app SET search_path = catalogo;
