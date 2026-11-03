-- sqlcmd -S "localhost,1433" -E -b -I -f 65001 -d EcommerceAuthDB -i 02_permisos.sql

GRANT SELECT, INSERT, UPDATE ON dbo.Users TO [Ecommerce-auth];
GRANT SELECT, INSERT, UPDATE ON dbo.UserCredentials TO [Ecommerce-auth];
GRANT SELECT ON dbo.Roles TO [Ecommerce-auth];
GRANT SELECT, INSERT, DELETE ON dbo.UserRoles TO [Ecommerce-auth];
GRANT SELECT, INSERT, UPDATE ON dbo.RefreshTokens TO [Ecommerce-auth];
GRANT SELECT, INSERT ON dbo.AuthAuditLogs TO [Ecommerce-auth];
GO
