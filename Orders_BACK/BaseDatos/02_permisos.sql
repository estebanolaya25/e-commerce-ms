-- sqlcmd -S "localhost,1433" -E -b -I -f 65001 -d EcommerceOrdersDB -i 02_permisos.sql

GRANT SELECT, INSERT, UPDATE ON ped.Pedidos TO [Ecommerce-orders];
GRANT SELECT, INSERT ON ped.PedidoItems TO [Ecommerce-orders];
GRANT SELECT, INSERT ON ped.PedidoHistorial TO [Ecommerce-orders];
GRANT SELECT, INSERT, UPDATE ON ped.Outbox TO [Ecommerce-orders];
GRANT SELECT, INSERT ON ped.MensajesProcesados TO [Ecommerce-orders];
GRANT UPDATE ON OBJECT::ped.SecuenciaPedidos TO [Ecommerce-orders];
GO
