-- Ejecutar con un usuario sysadmin. La contraseña se pasa como variable de sqlcmd, nunca se escribe aquí:
-- sqlcmd -S "localhost,1433" -E -b -f 65001 -v ClaveLogin="..." -i 00_crear_base_y_login.sql

IF DB_ID(N'EcommerceInventoryDB') IS NULL
    CREATE DATABASE EcommerceInventoryDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'Ecommerce-inventory')
    CREATE LOGIN [Ecommerce-inventory] WITH PASSWORD = N'$(ClaveLogin)', CHECK_POLICY = ON, DEFAULT_DATABASE = EcommerceInventoryDB;
GO

USE EcommerceInventoryDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'Ecommerce-inventory')
    CREATE USER [Ecommerce-inventory] FOR LOGIN [Ecommerce-inventory];
GO
