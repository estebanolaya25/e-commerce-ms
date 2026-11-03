-- sqlcmd -S "localhost,1433" -E -b -I -f 65001 -v ClaveLogin="..." -i 00_crear_base_y_login.sql

IF DB_ID(N'EcommercePaymentsDB') IS NULL
    CREATE DATABASE EcommercePaymentsDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'Ecommerce-payments')
    CREATE LOGIN [Ecommerce-payments] WITH PASSWORD = N'$(ClaveLogin)', CHECK_POLICY = ON, DEFAULT_DATABASE = EcommercePaymentsDB;
GO

USE EcommercePaymentsDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'Ecommerce-payments')
    CREATE USER [Ecommerce-payments] FOR LOGIN [Ecommerce-payments];
GO
