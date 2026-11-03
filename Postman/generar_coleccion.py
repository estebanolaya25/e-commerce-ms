"""Genera la colección de Postman del proyecto.

Uso:  python generar_coleccion.py
Escribe E-Commerce-ms.postman_collection.json en esta misma carpeta.
"""
import json
import os
import uuid

CARPETA = os.path.dirname(os.path.abspath(__file__))
DESTINO = os.path.join(CARPETA, "E-Commerce-ms.postman_collection.json")
IMAGEN = os.path.join(CARPETA, "imagen-prueba.png").replace("\\", "/")


def peticion(nombre, metodo, ruta, cuerpo=None, token=None, pruebas=None, previo=None, formulario=None, descripcion=None, cabeceras=None):
    req = {"method": metodo, "header": [{"key": k, "value": v} for k, v in (cabeceras or {}).items()], "url": "{{gateway}}" + ruta}
    if descripcion:
        req["description"] = descripcion
    if cuerpo is not None:
        req["body"] = {"mode": "raw", "raw": json.dumps(cuerpo, indent=2, ensure_ascii=False), "options": {"raw": {"language": "json"}}}
    if formulario is not None:
        req["body"] = {"mode": "formdata", "formdata": formulario}
    req["auth"] = {"type": "bearer", "bearer": [{"key": "token", "value": "{{" + token + "}}", "type": "string"}]} if token else {"type": "noauth"}
    eventos = []
    if previo:
        eventos.append({"listen": "prerequest", "script": {"type": "text/javascript", "exec": previo}})
    if pruebas:
        eventos.append({"listen": "test", "script": {"type": "text/javascript", "exec": pruebas}})
    item = {"name": nombre, "request": req}
    if eventos:
        item["event"] = eventos
    return item


def estado(codigo):
    return [f'pm.test("Responde {codigo}", () => pm.response.to.have.status({codigo}));']


def esperar(segundos, motivo):
    return [f"// {motivo}", f"setTimeout(() => {{}}, {segundos * 1000});"]


def tarjeta(variable):
    return {"tarjeta": {"numero": "{{" + variable + "}}", "titular": "CLIENTE POSTMAN", "mesVencimiento": 12, "anioVencimiento": 2030, "cvv": "123"}}


DIRECCION = {"direccion": {"nombreDestinatario": "Cliente Postman", "telefono": "3001234567", "direccion": "Calle 10 # 20-30",
                           "ciudad": "Medellín", "departamento": "Antioquia", "notas": "Dejar en portería"}}

carpetas = [
    {"name": "0. Salud", "item": [
        peticion("Gateway vivo", "GET", "/health", pruebas=estado(200) + [
            'pm.test("Dice Healthy", () => pm.expect(pm.response.text()).to.eql("Healthy"));']),
    ]},

    {"name": "1. Cliente nuevo (Auth)", "item": [
        peticion("Registro", "POST", "/api/auth/registro",
                 cuerpo={"nombre": "Cliente", "apellido": "Postman", "email": "{{correoCliente}}", "password": "Prueba123"},
                 previo=['pm.collectionVariables.set("correoCliente", `cliente.${Date.now()}@ejemplo.com`);'],
                 pruebas=estado(201) + [
                     'const r = pm.response.json();',
                     'pm.test("Trae access y refresh token", () => { pm.expect(r.access_token).to.be.a("string"); pm.expect(r.refresh_token).to.be.a("string"); });',
                     'pm.test("Rol Customer", () => pm.expect(r.roles).to.include("Customer"));',
                     'pm.collectionVariables.set("tokenCliente", r.access_token);']),
        peticion("Usuario actual", "GET", "/api/auth/current", token="tokenCliente", pruebas=estado(200) + [
            'pm.test("Es el usuario registrado", () => pm.expect(pm.response.json().email).to.eql(pm.collectionVariables.get("correoCliente")));']),
        peticion("Usuario actual sin token", "GET", "/api/auth/current", pruebas=estado(401)),
    ]},

    {"name": "2. Navegar el catálogo (público)", "item": [
        peticion("Árbol de categorías", "GET", "/api/catalogo/categorias", pruebas=estado(200) + [
            'const cats = pm.response.json();',
            'pm.test("Hay categorías raíz con subcategorías", () => pm.expect(cats.find(c => c.hijas.length > 0)).to.exist);',
            'pm.collectionVariables.set("subcategoriaId", cats.find(c => c.hijas.length > 0).hijas[0].id);']),
        peticion("Marcas", "GET", "/api/catalogo/marcas", pruebas=estado(200) + [
            'pm.test("Hay marcas activas", () => pm.expect(pm.response.json().length).to.be.above(0));']),
        peticion("Productos de tecnología (más caros primero)", "GET", "/api/catalogo/productos?categoria=tecnologia&orden=precio_desc&tamano=5",
                 pruebas=estado(200) + [
                     'const p = pm.response.json();',
                     'pm.test("Incluye productos de las subcategorías", () => pm.expect(p.total).to.be.above(0));',
                     'pm.test("Ordenado por precio descendente", () => pm.expect(p.items[0].precioDesde).to.be.at.least(p.items[p.items.length - 1].precioDesde));',
                     'pm.collectionVariables.set("slugProducto", p.items[0].slug);']),
        peticion("Detalle de producto", "GET", "/api/catalogo/productos/{{slugProducto}}", pruebas=estado(200) + [
            'const d = pm.response.json();',
            'pm.test("Tiene variantes", () => pm.expect(d.variantes.length).to.be.above(0));',
            'pm.test("Tiene imágenes", () => pm.expect(d.imagenes.length).to.be.above(0));',
            'pm.collectionVariables.set("urlImagen", d.imagenes[0].url);']),
        peticion("Ver imagen", "GET", "{{urlImagen}}", pruebas=estado(200) + [
            'pm.test("Es una imagen", () => pm.expect(pm.response.headers.get("Content-Type")).to.match(/^image\\//));',
            'pm.test("Con cabecera nosniff", () => pm.expect(pm.response.headers.get("X-Content-Type-Options")).to.eql("nosniff"));']),
        peticion("Búsqueda en español", "GET", "/api/catalogo/productos?buscar=jean tiro alto&orden=relevancia", pruebas=estado(200) + [
            'pm.test("Encuentra resultados", () => pm.expect(pm.response.json().total).to.be.above(0));']),
        peticion("Filtro por rango de precio", "GET", "/api/catalogo/productos?precioMin=100000&precioMax=150000", pruebas=estado(200) + [
            'pm.test("Todos dentro del rango", () => pm.response.json().items.forEach(i => pm.expect(i.precioDesde).to.be.within(100000, 150000)));']),
        peticion("Slug inexistente", "GET", "/api/catalogo/productos/no-existe-este-producto", pruebas=estado(404)),
    ]},

    {"name": "3. Seguridad entre servicios", "item": [
        peticion("Cliente intenta crear marca", "POST", "/api/catalogo/marcas", cuerpo={"nombre": "No debería"}, token="tokenCliente", pruebas=estado(403)),
        peticion("Sin token intenta crear marca", "POST", "/api/catalogo/marcas", cuerpo={"nombre": "No debería"}, pruebas=estado(401)),
        peticion("Cliente pide productos inactivos", "GET", "/api/catalogo/productos?incluirInactivos=true", token="tokenCliente", pruebas=estado(403)),
        peticion("Carrito sin token (lo frena el gateway)", "GET", "/api/carrito", pruebas=estado(401)),
        peticion("Cliente consulta stock de bodega", "GET", "/api/inventario/stock", token="tokenCliente", pruebas=estado(403)),
    ]},

    {"name": "4. Administrador crea un producto (Catálogo)", "item": [
        peticion("Login administrador", "POST", "/api/auth/login", cuerpo={"email": "{{adminEmail}}", "password": "{{adminPassword}}"},
                 pruebas=estado(200) + [
                     'const r = pm.response.json();',
                     'pm.test("Rol Administrator", () => pm.expect(r.roles).to.include("Administrator"));',
                     'pm.collectionVariables.set("tokenAdmin", r.access_token);',
                     'pm.collectionVariables.set("refreshAdmin", r.refresh_token);']),
        peticion("Crear marca", "POST", "/api/catalogo/marcas", cuerpo={"nombre": "Marca Postman {{sufijo}}"}, token="tokenAdmin",
                 previo=['pm.collectionVariables.set("sufijo", Date.now().toString());'],
                 pruebas=estado(201) + ['pm.collectionVariables.set("marcaId", pm.response.json().id);']),
        peticion("Marca duplicada", "POST", "/api/catalogo/marcas", cuerpo={"nombre": "Marca Postman {{sufijo}}"}, token="tokenAdmin", pruebas=estado(409)),
        peticion("Crear producto con variantes", "POST", "/api/catalogo/productos", token="tokenAdmin",
                 cuerpo={"categoriaId": "{{subcategoriaId}}", "marcaId": "{{marcaId}}", "nombre": "Producto Postman {{sufijo}}",
                         "descripcionCorta": "Creado desde Postman", "precioBase": 99900, "atributos": {"material": "Algodón"},
                         "variantes": [{"sku": "PM-{{sufijo}}-M", "opciones": {"talla": "M"}},
                                       {"sku": "PM-{{sufijo}}-L", "opciones": {"talla": "L"}, "precio": 89900, "precioAntes": 99900}]},
                 pruebas=estado(201) + [
                     'const p = pm.response.json();',
                     'pm.test("Creó 2 variantes", () => pm.expect(p.variantes.length).to.eql(2));',
                     'pm.collectionVariables.set("productoId", p.id);',
                     'pm.collectionVariables.set("skuM", p.variantes.find(v => v.sku.endsWith("-M")).sku);',
                     'pm.collectionVariables.set("skuL", p.variantes.find(v => v.sku.endsWith("-L")).sku);',
                     'pm.collectionVariables.set("varianteMId", p.variantes.find(v => v.sku.endsWith("-M")).id);']),
        peticion("El SKU no se puede cambiar", "PUT", "/api/catalogo/variantes/{{varianteMId}}", cuerpo={"sku": "OTRO-SKU", "opciones": {"talla": "M"}},
                 token="tokenAdmin", pruebas=estado(400)),
        peticion("Subir imagen", "POST", "/api/catalogo/productos/{{productoId}}/imagenes", token="tokenAdmin",
                 descripcion="Si Postman no encuentra el archivo, selecciona cualquier JPG, PNG o WEBP en el campo 'archivo'.",
                 formulario=[{"key": "archivo", "type": "file", "src": IMAGEN},
                             {"key": "textoAlternativo", "value": "Foto desde Postman", "type": "text"}],
                 pruebas=estado(201) + ['pm.test("Queda como principal", () => pm.expect(pm.response.json().esPrincipal).to.be.true);']),
        peticion("El público lo ve (caché invalidada)", "GET", "/api/catalogo/productos?buscar=Postman {{sufijo}}", pruebas=estado(200) + [
            'const p = pm.response.json();',
            'pm.test("Aparece de inmediato", () => pm.expect(p.total).to.eql(1));',
            'pm.test("Con su imagen", () => pm.expect(p.items[0].imagenUrl).to.be.a("string"));',
            'pm.test("Precio desde la variante con descuento", () => pm.expect(p.items[0].precioDesde).to.eql(89900));']),
    ]},

    {"name": "5. Bodega (Inventario + evento de RabbitMQ)", "item": [
        peticion("Stock creado por el evento VarianteCreada", "GET", "/api/inventario/disponibilidad?skus={{skuM}},{{skuL}}",
                 previo=esperar(2, "Da tiempo a que el evento viaje Catálogo -> RabbitMQ -> Inventario"),
                 pruebas=estado(200) + [
                     'const d = pm.response.json();',
                     'pm.test("Inventario ya conoce los 2 SKU (los creó al recibir el evento)", () => pm.expect(d.every(x => x.existe)).to.be.true);',
                     'pm.test("Arrancan agotados (stock 0)", () => pm.expect(d.every(x => x.agotado)).to.be.true);']),
        peticion("Login bodega (rol Warehouse)", "POST", "/api/auth/login", cuerpo={"email": "{{bodegaEmail}}", "password": "{{adminPassword}}"},
                 pruebas=estado(200) + [
                     'const r = pm.response.json();',
                     'pm.test("Rol Warehouse", () => pm.expect(r.roles).to.include("Warehouse"));',
                     'pm.collectionVariables.set("tokenBodega", r.access_token);']),
        peticion("Entrada de mercancía talla M", "POST", "/api/inventario/stock/{{skuM}}/entradas", {"cantidad": 20, "motivo": "Llegada de proveedor"},
                 token="tokenBodega", pruebas=estado(200) + ['pm.test("Quedan 20 disponibles", () => pm.expect(pm.response.json().cantidadDisponible).to.eql(20));']),
        peticion("Entrada de mercancía talla L", "POST", "/api/inventario/stock/{{skuL}}/entradas", {"cantidad": 5, "motivo": "Llegada de proveedor"},
                 token="tokenBodega", pruebas=estado(200)),
        peticion("Ajuste que dejaría stock negativo", "POST", "/api/inventario/stock/{{skuL}}/ajustes", {"cantidad": -100, "motivo": "Error de conteo"},
                 token="tokenBodega", pruebas=estado(400)),
        peticion("Historial de movimientos", "GET", "/api/inventario/stock/{{skuM}}/movimientos", token="tokenBodega", pruebas=estado(200) + [
            'const m = pm.response.json();',
            'pm.test("Registra el Alta (evento) y la Entrada", () => pm.expect(m.map(x => x.tipo)).to.include.members(["Alta", "Entrada"]));']),
        peticion("Disponibilidad pública (la usa Carrito)", "GET", "/api/inventario/disponibilidad?skus={{skuM}},{{skuL}},NO-EXISTE",
                 pruebas=estado(200) + [
                     'const d = pm.response.json();',
                     'pm.test("M disponible, L con pocas unidades, el otro no existe", () => {',
                     '  pm.expect(d[0].disponible).to.eql(20);',
                     '  pm.expect(d[1].pocasUnidades).to.be.true;',
                     '  pm.expect(d[2].existe).to.be.false;',
                     '});']),
    ]},

    {"name": "6. Carrito (Redis + Catálogo + Inventario)", "item": [
        peticion("Vaciar carrito", "DELETE", "/api/carrito", token="tokenCliente", pruebas=estado(204)),
        peticion("Agregar 2 unidades talla M", "POST", "/api/carrito/items", {"sku": "{{skuM}}", "cantidad": 2}, "tokenCliente", pruebas=estado(200) + [
            'const c = pm.response.json();',
            'pm.test("Trae nombre y precio desde Catálogo", () => { pm.expect(c.items[0].nombre).to.include("Postman"); pm.expect(c.items[0].precioUnitario).to.eql(99900); });',
            'pm.test("Disponible según Inventario", () => pm.expect(c.items[0].estado).to.eql("Disponible"));']),
        peticion("Agregar de nuevo suma la cantidad", "POST", "/api/carrito/items", {"sku": "{{skuM}}", "cantidad": 1}, "tokenCliente",
                 pruebas=estado(200) + ['pm.test("Ahora son 3", () => pm.expect(pm.response.json().items[0].cantidad).to.eql(3));']),
        peticion("Pasar de 10 unidades", "POST", "/api/carrito/items", {"sku": "{{skuM}}", "cantidad": 8}, "tokenCliente", pruebas=estado(409)),
        peticion("Pedir más de lo que hay en bodega", "POST", "/api/carrito/items", {"sku": "{{skuL}}", "cantidad": 6}, "tokenCliente",
                 pruebas=estado(409) + ['pm.test("Dice cuántas quedan", () => pm.expect(pm.response.json().mensaje).to.include("5"));']),
        peticion("Agregar 1 talla L", "POST", "/api/carrito/items", {"sku": "{{skuL}}", "cantidad": 1}, "tokenCliente", pruebas=estado(200)),
        peticion("Quitar la talla L", "DELETE", "/api/carrito/items/{{skuL}}", token="tokenCliente", pruebas=estado(200)),
        peticion("Ver carrito listo para comprar", "GET", "/api/carrito", token="tokenCliente", pruebas=estado(200) + [
            'const c = pm.response.json();',
            'pm.test("3 unidades de la talla M", () => pm.expect(c.unidades).to.eql(3));',
            'pm.test("Total = 3 x 99.900", () => pm.expect(c.total).to.eql(299700));',
            'pm.test("Puede comprar", () => pm.expect(c.puedeComprar).to.be.true);']),
    ]},

    {"name": "7. Compra y pago (Pedidos + Pagos + saga)", "item": [
        peticion("Checkout sin Idempotency-Key", "POST", "/api/pedidos", DIRECCION, "tokenCliente", pruebas=estado(400)),
        peticion("Checkout", "POST", "/api/pedidos", DIRECCION, "tokenCliente", cabeceras={"Idempotency-Key": "{{claveCompra}}"},
                 previo=['pm.collectionVariables.set("claveCompra", pm.variables.replaceIn("{{$guid}}"));'],
                 pruebas=estado(201) + [
                     'const p = pm.response.json();',
                     'pm.test("Pedido pendiente de pago", () => pm.expect(p.estado).to.eql("PendientePago"));',
                     'pm.test("Envío gratis (subtotal >= $150.000)", () => pm.expect(p.costoEnvio).to.eql(0));',
                     'pm.test("Guarda el precio del momento", () => pm.expect(p.items[0].precioUnitario).to.eql(99900));',
                     'pm.collectionVariables.set("pedidoId", p.pedidoId);',
                     'pm.collectionVariables.set("numeroPedido", p.numero);',
                     'console.log("Pedido creado:", p.numero, "total", p.total);']),
        peticion("Doble clic en Comprar (misma clave)", "POST", "/api/pedidos", DIRECCION, "tokenCliente", cabeceras={"Idempotency-Key": "{{claveCompra}}"},
                 pruebas=estado(200) + ['pm.test("Devuelve el mismo pedido", () => pm.expect(pm.response.json().pedidoId).to.eql(pm.collectionVariables.get("pedidoId")));']),
        peticion("El carrito se vació solo (evento PedidoCreado)", "GET", "/api/carrito", token="tokenCliente",
                 previo=esperar(2, "Outbox de Pedidos -> RabbitMQ -> consumidor de Carrito"),
                 pruebas=estado(200) + ['pm.test("Carrito vacío", () => pm.expect(pm.response.json().items.length).to.eql(0));']),
        peticion("Stock reservado en Inventario", "GET", "/api/inventario/stock/{{skuM}}", token="tokenBodega", pruebas=estado(200) + [
            'const s = pm.response.json();',
            'pm.test("3 reservadas, 17 disponibles", () => { pm.expect(s.cantidadReservada).to.eql(3); pm.expect(s.cantidadDisponible).to.eql(17); });']),
        peticion("Pagar con tarjeta que se rechaza", "POST", "/api/pedidos/{{pedidoId}}/pagar", tarjeta("tarjetaRechazada"), "tokenCliente",
                 pruebas=estado(202) + ['pm.test("Pago en proceso", () => pm.expect(pm.response.json().estadoPedido).to.eql("PagoEnProceso"));']),
        peticion("Resultado: rechazado", "GET", "/api/pedidos/{{pedidoId}}", token="tokenCliente",
                 previo=esperar(10, "El simulador de Pagos responde entre 2 y 8 segundos"),
                 pruebas=estado(200) + [
                     'const p = pm.response.json();',
                     'pm.test("Vuelve a PendientePago", () => pm.expect(p.estado).to.eql("PendientePago"));',
                     'pm.test("Con el motivo del rechazo", () => pm.expect(p.ultimoErrorPago).to.eql("Fondos insuficientes"));']),
        peticion("Pagar con tarjeta que se aprueba", "POST", "/api/pedidos/{{pedidoId}}/pagar", tarjeta("tarjetaAprobada"), "tokenCliente", pruebas=estado(202)),
        peticion("Doble clic en Pagar", "POST", "/api/pedidos/{{pedidoId}}/pagar", tarjeta("tarjetaAprobada"), "tokenCliente", pruebas=estado(409)),
        peticion("Resultado: pagado", "GET", "/api/pedidos/{{pedidoId}}", token="tokenCliente",
                 previo=esperar(10, "Espera el evento PagoAprobado"),
                 pruebas=estado(200) + [
                     'const p = pm.response.json();',
                     'pm.test("Pedido Pagado", () => pm.expect(p.estado).to.eql("Pagado"));',
                     'console.log("Historial:", p.historial.map(h => `${h.estadoNuevo}: ${h.motivo}`));']),
        peticion("Inventario descontó la venta", "GET", "/api/inventario/stock/{{skuM}}", token="tokenBodega", pruebas=estado(200) + [
            'const s = pm.response.json();',
            'pm.test("Quedan 17 físicas y 0 reservadas", () => { pm.expect(s.cantidadFisica).to.eql(17); pm.expect(s.cantidadReservada).to.eql(0); });']),
        peticion("Mis pedidos", "GET", "/api/pedidos", token="tokenCliente", pruebas=estado(200) + [
            'pm.test("Aparece el pedido", () => pm.expect(pm.response.json().items.map(p => p.numero)).to.include(pm.collectionVariables.get("numeroPedido")));']),
        peticion("Otro usuario no ve mi pedido", "GET", "/api/pedidos/{{pedidoId}}", token="tokenAdmin", pruebas=estado(404)),
        peticion("Intentos de pago registrados (admin)", "GET", "/api/pagos?pedidoId={{pedidoId}}", token="tokenAdmin", pruebas=estado(200) + [
            'const estados = pm.response.json().items.map(p => p.estado).sort();',
            'pm.test("Un rechazado y un aprobado", () => pm.expect(estados).to.eql(["Aprobado", "Rechazado"]));',
            'pm.test("Nunca guarda el número completo", () => pm.response.json().items.forEach(p => pm.expect(p.ultimos4).to.have.lengthOf(4)));']),
    ]},

    {"name": "8. Despacho (admin)", "item": [
        peticion("Cliente en endpoint de admin", "GET", "/api/pedidos/admin", token="tokenCliente", pruebas=estado(403)),
        peticion("Saltar de Pagado a Entregado", "PATCH", "/api/pedidos/{{pedidoId}}/estado", {"estado": "Entregado"}, "tokenAdmin", pruebas=estado(409)),
        peticion("En preparación", "PATCH", "/api/pedidos/{{pedidoId}}/estado", {"estado": "EnPreparacion"}, "tokenAdmin", pruebas=estado(200)),
        peticion("Enviado", "PATCH", "/api/pedidos/{{pedidoId}}/estado", {"estado": "Enviado", "motivo": "Guía 123456"}, "tokenAdmin", pruebas=estado(200)),
        peticion("Entregado", "PATCH", "/api/pedidos/{{pedidoId}}/estado", {"estado": "Entregado"}, "tokenAdmin", pruebas=estado(200) + [
            'pm.test("Estado final Entregado", () => pm.expect(pm.response.json().estado).to.eql("Entregado"));']),
    ]},

    {"name": "9. Compra cancelada (compensación)", "item": [
        peticion("Agregar 1 talla L ($89.900)", "POST", "/api/carrito/items", {"sku": "{{skuL}}", "cantidad": 1}, "tokenCliente", pruebas=estado(200)),
        peticion("Checkout", "POST", "/api/pedidos", DIRECCION, "tokenCliente", cabeceras={"Idempotency-Key": "{{claveCompra2}}"},
                 previo=['pm.collectionVariables.set("claveCompra2", pm.variables.replaceIn("{{$guid}}"));'],
                 pruebas=estado(201) + [
                     'const p = pm.response.json();',
                     'pm.test("Paga envío (subtotal < $150.000)", () => pm.expect(p.costoEnvio).to.eql(12000));',
                     'pm.collectionVariables.set("pedidoCanceladoId", p.pedidoId);']),
        peticion("Cancelar", "POST", "/api/pedidos/{{pedidoCanceladoId}}/cancelar", token="tokenCliente", pruebas=estado(200) + [
            'pm.test("Cancelado", () => pm.expect(pm.response.json().estado).to.eql("Cancelado"));']),
        peticion("El stock volvió", "GET", "/api/inventario/stock/{{skuL}}", token="tokenBodega", pruebas=estado(200) + [
            'pm.test("0 reservadas, 5 disponibles", () => { pm.expect(pm.response.json().cantidadReservada).to.eql(0); pm.expect(pm.response.json().cantidadDisponible).to.eql(5); });']),
        peticion("Pagar un pedido cancelado", "POST", "/api/pedidos/{{pedidoCanceladoId}}/pagar", tarjeta("tarjetaAprobada"), "tokenCliente", pruebas=estado(409)),
    ]},

    {"name": "10. Limpieza (admin)", "item": [
        peticion("Desactivar producto", "PATCH", "/api/catalogo/productos/{{productoId}}/estado", {"activo": False}, "tokenAdmin", pruebas=estado(204)),
        peticion("El público ya no lo ve", "GET", "/api/catalogo/productos?buscar=Postman {{sufijo}}", pruebas=estado(200) + [
            'pm.test("Oculto", () => pm.expect(pm.response.json().total).to.eql(0));']),
        peticion("Desactivar marca", "PATCH", "/api/catalogo/marcas/{{marcaId}}/estado", {"activo": False}, "tokenAdmin", pruebas=estado(204)),
    ]},

    {"name": "11. Sesión (refresh y logout)", "item": [
        peticion("Renovar token", "POST", "/api/auth/refresh", {"refreshToken": "{{refreshAdmin}}"}, pruebas=estado(200) + [
            'const r = pm.response.json();',
            'pm.test("Refresh token rotado", () => pm.expect(r.refresh_token).to.not.eql(pm.collectionVariables.get("refreshAdmin")));',
            'pm.collectionVariables.set("refreshNuevo", r.refresh_token);']),
        peticion("Cerrar sesión", "POST", "/api/auth/revoke", {"refreshToken": "{{refreshNuevo}}"}, pruebas=estado(200)),
        peticion("Refresh después del logout", "POST", "/api/auth/refresh", {"refreshToken": "{{refreshNuevo}}"}, pruebas=estado(401)),
        peticion("Reutilizar el refresh viejo", "POST", "/api/auth/refresh", {"refreshToken": "{{refreshAdmin}}"}, pruebas=estado(401)),
    ]},

    {"name": "12. Experimentos (ejecutar aparte)", "item": [
        peticion("Rate limit: login fallido", "POST", "/api/auth/login", {"email": "noexiste@ejemplo.com", "password": "Incorrecta1"},
                 descripcion="Ejecútala con el Runner en 25 iteraciones: desde la 21 el gateway responde 429.",
                 pruebas=['pm.test("401 o 429", () => pm.expect(pm.response.code).to.be.oneOf([401, 429]));', 'console.log("Respuesta:", pm.response.code);']),
        peticion("Pago aleatorio (80 % de aprobación)", "POST", "/api/pedidos/{{pedidoId}}/pagar", tarjeta("tarjetaAleatoria"), "tokenCliente",
                 descripcion="Haga antes un checkout nuevo (carpeta 7) y use su pedidoId. Esta tarjeta se aprueba el 80 % de las veces.",
                 pruebas=['pm.test("202 o 409", () => pm.expect(pm.response.code).to.be.oneOf([202, 409]));']),
    ]},
]

coleccion = {
    "info": {
        "_postman_id": str(uuid.uuid4()),
        "name": "E-Commerce-ms",
        "description": (
            "Recorrido de fin a fin por el gateway con los 7 microservicios.\n\n"
            "Ejecute las carpetas 0 a 11 en orden con el Runner (la 12 es aparte). Cada petición guarda tokens e ids en las variables "
            "de la colección. Algunas peticiones esperan unos segundos a propósito: los eventos de RabbitMQ y el simulador de pagos son asíncronos.\n\n"
            "Tarjetas de prueba: 4111 1111 1111 1111 (aprobada), 4000 0000 0000 0002 (rechazada), 5555 5555 5555 4444 (80 % aprobada)."),
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    },
    "item": carpetas,
    "variable": [
        {"key": "gateway", "value": "http://localhost:5100"},
        {"key": "adminEmail", "value": "admin@ejemplo.com"},
        {"key": "adminPassword", "value": "Demo12345"},
        {"key": "bodegaEmail", "value": "daniela.arango@ejemplo.com"},
        {"key": "tarjetaAprobada", "value": "4111 1111 1111 1111"},
        {"key": "tarjetaRechazada", "value": "4000 0000 0000 0002"},
        {"key": "tarjetaAleatoria", "value": "5555 5555 5555 4444"},
    ] + [{"key": k, "value": ""} for k in [
        "correoCliente", "tokenCliente", "subcategoriaId", "slugProducto", "urlImagen", "tokenAdmin", "refreshAdmin", "tokenBodega",
        "sufijo", "marcaId", "productoId", "varianteMId", "skuM", "skuL", "claveCompra", "pedidoId", "numeroPedido",
        "claveCompra2", "pedidoCanceladoId", "refreshNuevo"]],
}

with open(DESTINO, "w", encoding="utf-8") as f:
    json.dump(coleccion, f, indent=2, ensure_ascii=False)
print(f"Colección generada: {sum(len(c['item']) for c in carpetas)} peticiones en {len(carpetas)} carpetas -> {DESTINO}")
