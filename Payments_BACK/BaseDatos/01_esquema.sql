-- sqlcmd -S "localhost,1433" -E -b -I -f 65001 -d EcommercePaymentsDB -i 01_esquema.sql
-- Nunca se guarda el número completo de la tarjeta ni el CVV: solo marca y últimos 4 dígitos.

SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF SCHEMA_ID(N'pag') IS NULL
    EXEC (N'CREATE SCHEMA pag');
GO

BEGIN TRANSACTION;

CREATE TABLE pag.Pagos (
    PagoId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Pagos_Id DEFAULT (NEWSEQUENTIALID()),
    PedidoId       UNIQUEIDENTIFIER NOT NULL,
    Monto          DECIMAL(12,2)    NOT NULL,
    Moneda         CHAR(3)          NOT NULL CONSTRAINT DF_Pagos_Moneda DEFAULT ('COP'),
    Estado         VARCHAR(20)      NOT NULL CONSTRAINT DF_Pagos_Estado DEFAULT ('Procesando'),
    Marca          VARCHAR(20)      NOT NULL,
    Ultimos4       CHAR(4)          NOT NULL,
    Titular        NVARCHAR(100)    NOT NULL,
    Modo           VARCHAR(10)      NOT NULL,
    ProcesarEn     DATETIME2        NOT NULL,
    Referencia     VARCHAR(30)      NULL,
    MotivoRechazo  NVARCHAR(200)    NULL,
    CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Pagos_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ProcesadoEn    DATETIME2        NULL,
    ReembolsadoEn  DATETIME2        NULL,
    CONSTRAINT PK_Pagos PRIMARY KEY (PagoId),
    CONSTRAINT CK_Pagos_Estado CHECK (Estado IN ('Procesando', 'Aprobado', 'Rechazado', 'Reembolsado')),
    CONSTRAINT CK_Pagos_Modo CHECK (Modo IN ('Aprobar', 'Rechazar', 'Aleatorio')),
    CONSTRAINT CK_Pagos_Monto CHECK (Monto > 0)
);

CREATE TABLE pag.Outbox (
    OutboxId     UNIQUEIDENTIFIER NOT NULL,
    Tipo         VARCHAR(100)     NOT NULL,
    Contenido    NVARCHAR(MAX)    NOT NULL,
    CreadoEn     DATETIME2        NOT NULL CONSTRAINT DF_Outbox_CreadoEn DEFAULT (SYSUTCDATETIME()),
    PublicadoEn  DATETIME2        NULL,
    Intentos     INT              NOT NULL CONSTRAINT DF_Outbox_Intentos DEFAULT (0),
    UltimoError  NVARCHAR(500)    NULL,
    CONSTRAINT PK_Outbox PRIMARY KEY (OutboxId)
);

-- Un pedido solo puede tener un pago en curso o aprobado; los rechazados permiten reintentar con otra tarjeta.
CREATE UNIQUE INDEX UQ_Pagos_PedidoVigente ON pag.Pagos (PedidoId) WHERE Estado IN ('Procesando', 'Aprobado');
CREATE INDEX IX_Pagos_Pedido ON pag.Pagos (PedidoId, CreatedAt DESC);
CREATE INDEX IX_Pagos_PorProcesar ON pag.Pagos (ProcesarEn) WHERE Estado = 'Procesando';
CREATE INDEX IX_Outbox_Pendientes ON pag.Outbox (CreadoEn) WHERE PublicadoEn IS NULL;

COMMIT;
GO
