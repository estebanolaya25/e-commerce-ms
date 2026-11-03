-- sqlcmd -S "localhost,1433" -E -b -f 65001 -d EcommerceInventoryDB -i 01_esquema.sql

SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF SCHEMA_ID(N'inv') IS NULL
    EXEC (N'CREATE SCHEMA inv');
GO

BEGIN TRANSACTION;

CREATE TABLE inv.Stock (
    Sku                 NVARCHAR(50)     NOT NULL,
    ProductoId          UNIQUEIDENTIFIER NULL,
    VarianteId          UNIQUEIDENTIFIER NULL,
    CantidadFisica      INT              NOT NULL CONSTRAINT DF_Stock_Fisica DEFAULT (0),
    CantidadReservada   INT              NOT NULL CONSTRAINT DF_Stock_Reservada DEFAULT (0),
    CantidadDisponible  AS (CantidadFisica - CantidadReservada) PERSISTED,
    StockMinimo         INT              NOT NULL CONSTRAINT DF_Stock_Minimo DEFAULT (5),
    RowVersion          ROWVERSION       NOT NULL,
    CreatedAt           DATETIME2        NOT NULL CONSTRAINT DF_Stock_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2        NOT NULL CONSTRAINT DF_Stock_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Stock PRIMARY KEY (Sku),
    CONSTRAINT CK_Stock_Fisica CHECK (CantidadFisica >= 0),
    CONSTRAINT CK_Stock_Reservada CHECK (CantidadReservada >= 0 AND CantidadReservada <= CantidadFisica),
    CONSTRAINT CK_Stock_Minimo CHECK (StockMinimo >= 0)
);

CREATE TABLE inv.Reservas (
    ReservaId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Reservas_Id DEFAULT (NEWSEQUENTIALID()),
    PedidoId    UNIQUEIDENTIFIER NOT NULL,
    Estado      VARCHAR(20)      NOT NULL CONSTRAINT DF_Reservas_Estado DEFAULT ('Activa'),
    ExpiraEn    DATETIME2        NOT NULL,
    CreatedAt   DATETIME2        NOT NULL CONSTRAINT DF_Reservas_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt   DATETIME2        NOT NULL CONSTRAINT DF_Reservas_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Reservas PRIMARY KEY (ReservaId),
    CONSTRAINT UQ_Reservas_Pedido UNIQUE (PedidoId),
    CONSTRAINT CK_Reservas_Estado CHECK (Estado IN ('Activa', 'Confirmada', 'Liberada', 'Expirada'))
);

CREATE TABLE inv.ReservaItems (
    ReservaId   UNIQUEIDENTIFIER NOT NULL,
    Sku         NVARCHAR(50)     NOT NULL,
    Cantidad    INT              NOT NULL,
    CONSTRAINT PK_ReservaItems PRIMARY KEY (ReservaId, Sku),
    CONSTRAINT FK_ReservaItems_Reserva FOREIGN KEY (ReservaId) REFERENCES inv.Reservas (ReservaId),
    CONSTRAINT FK_ReservaItems_Stock FOREIGN KEY (Sku) REFERENCES inv.Stock (Sku),
    CONSTRAINT CK_ReservaItems_Cantidad CHECK (Cantidad > 0)
);

CREATE TABLE inv.Movimientos (
    MovimientoId         BIGINT IDENTITY(1,1) NOT NULL,
    Sku                  NVARCHAR(50)     NOT NULL,
    Tipo                 VARCHAR(20)      NOT NULL,
    Cantidad             INT              NOT NULL,
    FisicaResultante     INT              NOT NULL,
    ReservadaResultante  INT              NOT NULL,
    ReferenciaId         UNIQUEIDENTIFIER NULL,
    Motivo               NVARCHAR(250)    NULL,
    UsuarioId            UNIQUEIDENTIFIER NULL,
    CreatedAt            DATETIME2        NOT NULL CONSTRAINT DF_Movimientos_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Movimientos PRIMARY KEY (MovimientoId),
    CONSTRAINT FK_Movimientos_Stock FOREIGN KEY (Sku) REFERENCES inv.Stock (Sku),
    CONSTRAINT CK_Movimientos_Tipo CHECK (Tipo IN ('Alta', 'Entrada', 'Ajuste', 'Reserva', 'Liberacion', 'Expiracion', 'Venta')),
    CONSTRAINT CK_Movimientos_Cantidad CHECK (Cantidad <> 0 OR Tipo = 'Alta')
);

CREATE TABLE inv.MensajesProcesados (
    MensajeId    UNIQUEIDENTIFIER NOT NULL,
    TipoMensaje  VARCHAR(100)     NOT NULL,
    ProcesadoEn  DATETIME2        NOT NULL CONSTRAINT DF_Mensajes_ProcesadoEn DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_MensajesProcesados PRIMARY KEY (MensajeId)
);

CREATE INDEX IX_Stock_Producto ON inv.Stock (ProductoId);
CREATE INDEX IX_Stock_Bajo ON inv.Stock (CantidadDisponible) INCLUDE (StockMinimo);
CREATE INDEX IX_Reservas_Expiracion ON inv.Reservas (ExpiraEn) WHERE Estado = 'Activa';
CREATE INDEX IX_ReservaItems_Sku ON inv.ReservaItems (Sku);
CREATE INDEX IX_Movimientos_Sku ON inv.Movimientos (Sku, MovimientoId DESC);
CREATE INDEX IX_Movimientos_Referencia ON inv.Movimientos (ReferenciaId) WHERE ReferenciaId IS NOT NULL;

COMMIT;
GO
