-- sqlcmd -S "localhost,1433" -E -b -I -f 65001 -d EcommerceAuthDB -i 01_esquema.sql

SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;

CREATE TABLE dbo.Users (
    UserId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_UserId DEFAULT (NEWSEQUENTIALID()),
    FirstName        NVARCHAR(100)    NOT NULL,
    LastName         NVARCHAR(100)    NOT NULL,
    Email            NVARCHAR(254)    NOT NULL,
    NormalizedEmail  NVARCHAR(254)    NOT NULL,
    IsEmailVerified  BIT              NOT NULL CONSTRAINT DF_Users_IsEmailVerified DEFAULT (0),
    IsActive         BIT              NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAt        DATETIME2(3)     NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt        DATETIME2(3)     NOT NULL CONSTRAINT DF_Users_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT UQ_Users_NormalizedEmail UNIQUE (NormalizedEmail),
    CONSTRAINT CK_Users_Email_NotEmpty CHECK (LEN(LTRIM(RTRIM(Email))) > 0)
);

CREATE INDEX IX_Users_IsActive ON dbo.Users (IsActive);

-- La contraseña vive aparte de los datos del usuario: hash PBKDF2 y control de bloqueo por intentos fallidos.
CREATE TABLE dbo.UserCredentials (
    UserId               UNIQUEIDENTIFIER NOT NULL,
    PasswordHash         NVARCHAR(500)    NOT NULL,
    PasswordChangedAt    DATETIME2(3)     NOT NULL CONSTRAINT DF_UserCredentials_PasswordChangedAt DEFAULT (SYSUTCDATETIME()),
    FailedLoginAttempts  INT              NOT NULL CONSTRAINT DF_UserCredentials_FailedAttempts DEFAULT (0),
    LockoutEnd           DATETIME2(3)     NULL,
    CONSTRAINT PK_UserCredentials PRIMARY KEY (UserId),
    CONSTRAINT FK_UserCredentials_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
    CONSTRAINT CK_UserCredentials_FailedAttempts CHECK (FailedLoginAttempts >= 0)
);

CREATE TABLE dbo.Roles (
    RoleId          INT IDENTITY(1,1) NOT NULL,
    Name            NVARCHAR(50)      NOT NULL,
    NormalizedName  NVARCHAR(50)      NOT NULL,
    Description     NVARCHAR(250)     NULL,
    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_NormalizedName UNIQUE (NormalizedName)
);

CREATE TABLE dbo.UserRoles (
    UserId      UNIQUEIDENTIFIER NOT NULL,
    RoleId      INT              NOT NULL,
    AssignedAt  DATETIME2(3)     NOT NULL CONSTRAINT DF_UserRoles_AssignedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId) ON DELETE CASCADE
);

CREATE INDEX IX_UserRoles_RoleId ON dbo.UserRoles (RoleId);

-- Solo se guarda el hash SHA-256 del refresh token. TokenFamilyId agrupa las rotaciones de una misma sesión:
-- si se reutiliza un token ya rotado, se revoca toda la familia (detección de robo).
CREATE TABLE dbo.RefreshTokens (
    RefreshTokenId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_RefreshTokens_RefreshTokenId DEFAULT (NEWSEQUENTIALID()),
    UserId             UNIQUEIDENTIFIER NOT NULL,
    TokenHash          VARBINARY(32)    NOT NULL,
    TokenFamilyId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_RefreshTokens_FamilyId DEFAULT (NEWID()),
    CreatedAt          DATETIME2(3)     NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt          DATETIME2(3)     NOT NULL,
    RevokedAt          DATETIME2(3)     NULL,
    ReplacedByTokenId  UNIQUEIDENTIFIER NULL,
    CreatedByIp        NVARCHAR(45)     NULL,
    RevokedByIp        NVARCHAR(45)     NULL,
    CONSTRAINT PK_RefreshTokens PRIMARY KEY (RefreshTokenId),
    CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_RefreshTokens_ReplacedBy FOREIGN KEY (ReplacedByTokenId) REFERENCES dbo.RefreshTokens (RefreshTokenId),
    CONSTRAINT CK_RefreshTokens_Expiration CHECK (ExpiresAt > CreatedAt)
);

CREATE INDEX IX_RefreshTokens_UserId ON dbo.RefreshTokens (UserId);
CREATE INDEX IX_RefreshTokens_TokenFamilyId ON dbo.RefreshTokens (TokenFamilyId);

-- Reservada para la recuperación de contraseña por correo (todavía no implementada en la API).
CREATE TABLE dbo.PasswordResetTokens (
    PasswordResetTokenId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PasswordResetTokens_Id DEFAULT (NEWSEQUENTIALID()),
    UserId                UNIQUEIDENTIFIER NOT NULL,
    TokenHash             VARBINARY(32)    NOT NULL,
    CreatedAt             DATETIME2(3)     NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt             DATETIME2(3)     NOT NULL,
    UsedAt                DATETIME2(3)     NULL,
    CONSTRAINT PK_PasswordResetTokens PRIMARY KEY (PasswordResetTokenId),
    CONSTRAINT UQ_PasswordResetTokens_TokenHash UNIQUE (TokenHash),
    CONSTRAINT FK_PasswordResetTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE CASCADE,
    CONSTRAINT CK_PasswordResetTokens_Expiration CHECK (ExpiresAt > CreatedAt)
);

CREATE INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens (UserId);

-- Auditoría de eventos de seguridad (login, bloqueo, refresh, cambios de rol). Si se borra el usuario, el registro se conserva.
CREATE TABLE dbo.AuthAuditLogs (
    AuthAuditLogId  BIGINT IDENTITY(1,1) NOT NULL,
    UserId          UNIQUEIDENTIFIER     NULL,
    EventType       NVARCHAR(50)         NOT NULL,
    IsSuccessful    BIT                  NOT NULL,
    IpAddress       NVARCHAR(45)         NULL,
    UserAgent       NVARCHAR(500)        NULL,
    Details         NVARCHAR(1000)       NULL,
    CreatedAt       DATETIME2(3)         NOT NULL CONSTRAINT DF_AuthAuditLogs_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AuthAuditLogs PRIMARY KEY (AuthAuditLogId),
    CONSTRAINT FK_AuthAuditLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId) ON DELETE SET NULL
);

CREATE INDEX IX_AuthAuditLogs_UserId_CreatedAt ON dbo.AuthAuditLogs (UserId, CreatedAt DESC);
CREATE INDEX IX_AuthAuditLogs_EventType_CreatedAt ON dbo.AuthAuditLogs (EventType, CreatedAt DESC);

-- Roles base. El registro público asigna Customer; los demás se asignan desde el panel de administración.
INSERT INTO dbo.Roles (Name, NormalizedName, Description) VALUES
    (N'Customer',       N'CUSTOMER',       N'Cliente de la tienda'),
    (N'Administrator',  N'ADMINISTRATOR',  N'Administrador de la plataforma'),
    (N'Support',        N'SUPPORT',        N'Personal de soporte'),
    (N'CatalogManager', N'CATALOGMANAGER', N'Gestiona productos, categorías y marcas del catálogo'),
    (N'Warehouse',      N'WAREHOUSE',      N'Personal de bodega e inventario');

COMMIT;
GO
