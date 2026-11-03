-- sqlcmd -S "localhost,1433" -E -b -I -f 65001 -d EcommercePaymentsDB -i 02_permisos.sql

GRANT SELECT, INSERT, UPDATE ON pag.Pagos TO [Ecommerce-payments];
GRANT SELECT, INSERT, UPDATE ON pag.Outbox TO [Ecommerce-payments];
GO
