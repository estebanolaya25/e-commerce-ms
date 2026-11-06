# Envíos

<!-- Valores de Pedidos__EnvioGratisDesde y Pedidos__CostoEnvio en docker-compose.yml. -->

## Costo del envío
El envío cuesta $12.000 COP por pedido. Si el subtotal de los productos es de $150.000 COP o más, el envío es gratis.

## Cómo se calcula el envío gratis
El envío gratis se calcula sobre el subtotal de los productos del pedido (sin contar el envío). Ejemplos:
- Subtotal $149.900: no alcanza el mínimo, se paga envío. Total = 149.900 + 12.000 = $161.900.
- Subtotal $150.000 o más: el envío es $0.
El carrito y el checkout muestran cuánto falta para el envío gratis antes de pagar.

## Cuándo se despacha un pedido
Un pedido se prepara y se despacha solo después de que el pago fue aprobado. Las etapas que ve el cliente son: Pagado → En preparación → Enviado → Entregado. Cada cambio queda registrado en la línea de tiempo del pedido, en «Mis pedidos».

## Tiempos de entrega
<!-- BORRADOR: el sistema no define tiempos de entrega. Cuando el negocio los defina, reemplace este párrafo
     (p. ej. "Bogotá: 1 a 2 días hábiles; resto del país: 3 a 5 días hábiles"). -->
La tienda aún no tiene publicados tiempos de entrega por ciudad. El avance del envío se puede seguir en la línea de tiempo del pedido, en «Mis pedidos».
