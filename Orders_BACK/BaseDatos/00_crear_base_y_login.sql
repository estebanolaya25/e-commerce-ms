-- sqlcmd -S "localhost,1433" -E -b -I -f 65001 -v ClaveLogin="..." -i 00_crear_base_y_login.sql

IF DB_ID(N'EcommerceOrdersDB') IS NULL
    CREATE DATABASE EcommerceOrdersDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'Ecommerce-orders')
    CREATE LOGIN [Ecommerce-orders] WITH PASSWORD = N'$(ClaveLogin)', CHECK_POLICY = ON, DEFAULT_DATABASE = EcommerceOrdersDB;
GO

USE EcommerceOrdersDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'Ecommerce-orders')
    CREATE USER [Ecommerce-orders] FOR LOGIN [Ecommerce-orders];
GO
