-- Ejecutar conectado a la base "postgres" con un superusuario.
-- La contraseña de catalog_app se pasa como variable, nunca se escribe en este archivo:
-- psql -U postgres -h localhost -v clave_app="..." -f 00_crear_base_y_usuario.sql

CREATE ROLE catalog_app WITH LOGIN PASSWORD :'clave_app';

CREATE DATABASE ecommerce_catalog
    WITH ENCODING 'UTF8'
    TEMPLATE template0;

REVOKE ALL ON DATABASE ecommerce_catalog FROM PUBLIC;
GRANT CONNECT ON DATABASE ecommerce_catalog TO catalog_app;
