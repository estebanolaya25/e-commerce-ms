-- Ejecutar con un usuario sysadmin. La contraseña se pasa como variable de sqlcmd, nunca se escribe aquí:
-- sqlcmd -S "localhost,1433" -E -b -I -f 65001 -v ClaveLogin="..." -i 00_crear_base_y_login.sql

IF DB_ID(N'EcommerceAuthDB') IS NULL
    CREATE DATABASE EcommerceAuthDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'Ecommerce-auth')
    CREATE LOGIN [Ecommerce-auth] WITH PASSWORD = N'$(ClaveLogin)', CHECK_POLICY = ON, DEFAULT_DATABASE = EcommerceAuthDB;
GO

USE EcommerceAuthDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'Ecommerce-auth')
    CREATE USER [Ecommerce-auth] FOR LOGIN [Ecommerce-auth];
GO
