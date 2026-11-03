-- sqlcmd -S "localhost,1433" -E -b -f 65001 -d EcommerceInventoryDB -i 02_permisos.sql
-- El usuario de la API nunca borra: el historial de movimientos solo admite INSERT.

GRANT SELECT, INSERT, UPDATE ON inv.Stock TO [Ecommerce-inventory];
GRANT SELECT, INSERT, UPDATE ON inv.Reservas TO [Ecommerce-inventory];
GRANT SELECT, INSERT ON inv.ReservaItems TO [Ecommerce-inventory];
GRANT SELECT, INSERT ON inv.Movimientos TO [Ecommerce-inventory];
GRANT SELECT, INSERT ON inv.MensajesProcesados TO [Ecommerce-inventory];
GO
