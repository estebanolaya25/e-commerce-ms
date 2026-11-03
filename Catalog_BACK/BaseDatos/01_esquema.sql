-- Ejecutar conectado a la base "ecommerce_catalog" con un superusuario:
-- psql -U postgres -h localhost -d ecommerce_catalog -f 01_esquema.sql

BEGIN;

CREATE SCHEMA catalogo;

CREATE FUNCTION catalogo.fn_actualizar_fecha()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.actualizado_en := now();
    RETURN NEW;
END
$$;

CREATE TABLE catalogo.categorias (
    id                  uuid         PRIMARY KEY DEFAULT uuidv7(),
    categoria_padre_id  uuid         NULL REFERENCES catalogo.categorias (id),
    nombre              varchar(100) NOT NULL,
    slug                varchar(120) NOT NULL,
    descripcion         text         NULL,
    orden               int          NOT NULL DEFAULT 0,
    activa              boolean      NOT NULL DEFAULT true,
    creado_en           timestamptz  NOT NULL DEFAULT now(),
    actualizado_en      timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_categorias_slug UNIQUE (slug),
    CONSTRAINT ck_categorias_padre CHECK (categoria_padre_id IS NULL OR categoria_padre_id <> id)
);

CREATE TABLE catalogo.marcas (
    id              uuid         PRIMARY KEY DEFAULT uuidv7(),
    nombre          varchar(100) NOT NULL,
    slug            varchar(120) NOT NULL,
    ruta_logo       varchar(500) NULL,
    activa          boolean      NOT NULL DEFAULT true,
    creado_en       timestamptz  NOT NULL DEFAULT now(),
    actualizado_en  timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_marcas_nombre UNIQUE (nombre),
    CONSTRAINT uq_marcas_slug UNIQUE (slug)
);

CREATE TABLE catalogo.productos (
    id                 uuid          PRIMARY KEY DEFAULT uuidv7(),
    categoria_id       uuid          NOT NULL REFERENCES catalogo.categorias (id),
    marca_id           uuid          NULL REFERENCES catalogo.marcas (id),
    nombre             varchar(200)  NOT NULL,
    slug               varchar(220)  NOT NULL,
    descripcion_corta  varchar(500)  NULL,
    descripcion        text          NULL,
    precio_base        numeric(12,2) NOT NULL,
    atributos          jsonb         NOT NULL DEFAULT '{}'::jsonb,
    activo             boolean       NOT NULL DEFAULT true,
    destacado          boolean       NOT NULL DEFAULT false,
    creado_por         uuid          NULL,
    actualizado_por    uuid          NULL,
    creado_en          timestamptz   NOT NULL DEFAULT now(),
    actualizado_en     timestamptz   NOT NULL DEFAULT now(),
    busqueda           tsvector GENERATED ALWAYS AS (
                           setweight(to_tsvector('spanish', coalesce(nombre, '')), 'A') ||
                           setweight(to_tsvector('spanish', coalesce(descripcion_corta, '')), 'B') ||
                           setweight(to_tsvector('spanish', coalesce(descripcion, '')), 'C')
                       ) STORED,
    CONSTRAINT uq_productos_slug UNIQUE (slug),
    CONSTRAINT ck_productos_precio CHECK (precio_base >= 0),
    CONSTRAINT ck_productos_atributos CHECK (jsonb_typeof(atributos) = 'object')
);

CREATE TABLE catalogo.producto_variantes (
    id              uuid          PRIMARY KEY DEFAULT uuidv7(),
    producto_id     uuid          NOT NULL REFERENCES catalogo.productos (id),
    sku             varchar(50)   NOT NULL,
    opciones        jsonb         NOT NULL DEFAULT '{}'::jsonb,
    precio          numeric(12,2) NULL,
    precio_antes    numeric(12,2) NULL,
    activa          boolean       NOT NULL DEFAULT true,
    creado_en       timestamptz   NOT NULL DEFAULT now(),
    actualizado_en  timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT uq_variantes_sku UNIQUE (sku),
    CONSTRAINT uq_variantes_producto_opciones UNIQUE (producto_id, opciones),
    CONSTRAINT ck_variantes_precio CHECK (precio IS NULL OR precio >= 0),
    CONSTRAINT ck_variantes_precio_antes CHECK (precio_antes IS NULL OR precio IS NULL OR precio_antes > precio),
    CONSTRAINT ck_variantes_opciones CHECK (jsonb_typeof(opciones) = 'object')
);

CREATE TABLE catalogo.producto_imagenes (
    id                 uuid         PRIMARY KEY DEFAULT uuidv7(),
    producto_id        uuid         NOT NULL REFERENCES catalogo.productos (id) ON DELETE CASCADE,
    variante_id        uuid         NULL REFERENCES catalogo.producto_variantes (id) ON DELETE CASCADE,
    ruta_archivo       varchar(500) NOT NULL,
    texto_alternativo  varchar(200) NULL,
    orden              int          NOT NULL DEFAULT 0,
    es_principal       boolean      NOT NULL DEFAULT false,
    creado_en          timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_imagenes_ruta UNIQUE (ruta_archivo)
);

CREATE INDEX ix_categorias_padre ON catalogo.categorias (categoria_padre_id);
CREATE INDEX ix_productos_categoria_activo ON catalogo.productos (categoria_id, activo);
CREATE INDEX ix_productos_marca ON catalogo.productos (marca_id);
CREATE INDEX ix_productos_destacado ON catalogo.productos (destacado) WHERE activo AND destacado;
CREATE INDEX ix_productos_busqueda ON catalogo.productos USING gin (busqueda);
CREATE INDEX ix_productos_atributos ON catalogo.productos USING gin (atributos jsonb_path_ops);
CREATE INDEX ix_variantes_producto ON catalogo.producto_variantes (producto_id);
CREATE INDEX ix_variantes_opciones ON catalogo.producto_variantes USING gin (opciones jsonb_path_ops);
CREATE INDEX ix_imagenes_producto ON catalogo.producto_imagenes (producto_id, orden);
CREATE UNIQUE INDEX uq_imagenes_principal ON catalogo.producto_imagenes (producto_id) WHERE es_principal;

CREATE TRIGGER tr_categorias_actualizado BEFORE UPDATE ON catalogo.categorias
    FOR EACH ROW EXECUTE FUNCTION catalogo.fn_actualizar_fecha();
CREATE TRIGGER tr_marcas_actualizado BEFORE UPDATE ON catalogo.marcas
    FOR EACH ROW EXECUTE FUNCTION catalogo.fn_actualizar_fecha();
CREATE TRIGGER tr_productos_actualizado BEFORE UPDATE ON catalogo.productos
    FOR EACH ROW EXECUTE FUNCTION catalogo.fn_actualizar_fecha();
CREATE TRIGGER tr_variantes_actualizado BEFORE UPDATE ON catalogo.producto_variantes
    FOR EACH ROW EXECUTE FUNCTION catalogo.fn_actualizar_fecha();

COMMIT;
