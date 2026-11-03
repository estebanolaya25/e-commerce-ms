-- sqlcmd -S "localhost,1433" -E -b -I -f 65001 -d EcommerceOrdersDB -i 01_esquema.sql

SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF SCHEMA_ID(N'ped') IS NULL
    EXEC (N'CREATE SCHEMA ped');
GO

BEGIN TRANSACTION;

CREATE SEQUENCE ped.SecuenciaPedidos AS BIGINT START WITH 1 INCREMENT BY 1;

CREATE TABLE ped.Pedidos (
    PedidoId              UNIQUEIDENTIFIER NOT NULL,
    Numero                VARCHAR(20)      NOT NULL,
    UsuarioId             UNIQUEIDENTIFIER NOT NULL,
    UsuarioEmail          NVARCHAR(254)    NULL,
    IdempotencyKey        VARCHAR(64)      NOT NULL,
    Estado                VARCHAR(20)      NOT NULL,
    Subtotal              DECIMAL(12,2)    NOT NULL,
    CostoEnvio            DECIMAL(12,2)    NOT NULL,
    Total                 DECIMAL(12,2)    NOT NULL,
    Moneda                CHAR(3)          NOT NULL CONSTRAINT DF_Pedidos_Moneda DEFAULT ('COP'),
    DestinatarioNombre    NVARCHAR(150)    NOT NULL,
    DestinatarioTelefono  VARCHAR(20)      NOT NULL,
    Direccion             NVARCHAR(250)    NOT NULL,
    Ciudad                NVARCHAR(100)    NOT NULL,
    Departamento          NVARCHAR(100)    NOT NULL,
    NotasEntrega          NVARCHAR(300)    NULL,
    PagoId                UNIQUEIDENTIFIER NULL,
    UltimoErrorPago       NVARCHAR(200)    NULL,
    ExpiraEn              DATETIME2        NOT NULL,
    CreatedAt             DATETIME2        NOT NULL CONSTRAINT DF_Pedidos_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt             DATETIME2        NOT NULL CONSTRAINT DF_Pedidos_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    RowVersion            ROWVERSION       NOT NULL,
    CONSTRAINT PK_Pedidos PRIMARY KEY (PedidoId),
    CONSTRAINT UQ_Pedidos_Numero UNIQUE (Numero),
    CONSTRAINT UQ_Pedidos_Idempotencia UNIQUE (UsuarioId, IdempotencyKey),
    CONSTRAINT CK_Pedidos_Estado CHECK (Estado IN ('PendientePago', 'PagoEnProceso', 'Pagado', 'EnPreparacion', 'Enviado', 'Entregado', 'Cancelado', 'Expirado')),
    CONSTRAINT CK_Pedidos_Montos CHECK (Subtotal > 0 AND CostoEnvio >= 0 AND Total = Subtotal + CostoEnvio)
);

CREATE TABLE ped.PedidoItems (
    PedidoId        UNIQUEIDENTIFIER NOT NULL,
    Sku             NVARCHAR(50)     NOT NULL,
    Nombre          NVARCHAR(200)    NOT NULL,
    Opciones        NVARCHAR(500)    NULL,
    ImagenUrl       NVARCHAR(500)    NULL,
    PrecioUnitario  DECIMAL(12,2)    NOT NULL,
    Cantidad        INT              NOT NULL,
    Subtotal        DECIMAL(12,2)    NOT NULL,
    CONSTRAINT PK_PedidoItems PRIMARY KEY (PedidoId, Sku),
    CONSTRAINT FK_PedidoItems_Pedido FOREIGN KEY (PedidoId) REFERENCES ped.Pedidos (PedidoId),
    CONSTRAINT CK_PedidoItems_Cantidad CHECK (Cantidad > 0),
    CONSTRAINT CK_PedidoItems_Subtotal CHECK (Subtotal = PrecioUnitario * Cantidad)
);

CREATE TABLE ped.PedidoHistorial (
    HistorialId     BIGINT IDENTITY(1,1) NOT NULL,
    PedidoId        UNIQUEIDENTIFIER NOT NULL,
    EstadoAnterior  VARCHAR(20)      NULL,
    EstadoNuevo     VARCHAR(20)      NOT NULL,
    Motivo          NVARCHAR(300)    NULL,
    UsuarioId       UNIQUEIDENTIFIER NULL,
    CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_Historial_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_PedidoHistorial PRIMARY KEY (HistorialId),
    CONSTRAINT FK_PedidoHistorial_Pedido FOREIGN KEY (PedidoId) REFERENCES ped.Pedidos (PedidoId)
);

-- Patrón Outbox: los eventos se guardan en la misma transacción que el cambio de negocio
-- y una tarea de fondo los publica después en RabbitMQ.
CREATE TABLE ped.Outbox (
    OutboxId     UNIQUEIDENTIFIER NOT NULL,
    Tipo         VARCHAR(100)     NOT NULL,
    Contenido    NVARCHAR(MAX)    NOT NULL,
    CreadoEn     DATETIME2        NOT NULL CONSTRAINT DF_Outbox_CreadoEn DEFAULT (SYSUTCDATETIME()),
    PublicadoEn  DATETIME2        NULL,
    Intentos     INT              NOT NULL CONSTRAINT DF_Outbox_Intentos DEFAULT (0),
    UltimoError  NVARCHAR(500)    NULL,
    CONSTRAINT PK_Outbox PRIMARY KEY (OutboxId)
);

CREATE TABLE ped.MensajesProcesados (
    MensajeId    UNIQUEIDENTIFIER NOT NULL,
    TipoMensaje  VARCHAR(100)     NOT NULL,
    ProcesadoEn  DATETIME2        NOT NULL CONSTRAINT DF_Mensajes_ProcesadoEn DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_MensajesProcesados PRIMARY KEY (MensajeId)
);

CREATE INDEX IX_Pedidos_Usuario ON ped.Pedidos (UsuarioId, CreatedAt DESC);
CREATE INDEX IX_Pedidos_Estado ON ped.Pedidos (Estado, CreatedAt DESC);
CREATE INDEX IX_Pedidos_Expiracion ON ped.Pedidos (ExpiraEn) WHERE Estado IN ('PendientePago', 'PagoEnProceso');
CREATE INDEX IX_Historial_Pedido ON ped.PedidoHistorial (PedidoId, HistorialId);
CREATE INDEX IX_Outbox_Pendientes ON ped.Outbox (CreadoEn) WHERE PublicadoEn IS NULL;

COMMIT;
GO
