"""Genera datos de ejemplo para Auth (SQL Server) y Catálogo (PostgreSQL).

Uso:  python generar_semillas.py
Salida en ./salida:  auth.sql, catalogo.sql e imagenes/productos/...
"""
import base64
import hashlib
import json
import os
import random
import struct
import time
import unicodedata
import uuid
import zlib
from datetime import datetime, timedelta, timezone

random.seed(2026)

RAIZ = os.path.dirname(os.path.abspath(__file__))
SALIDA = os.path.join(RAIZ, "salida")
CARPETA_IMAGENES = os.path.join(SALIDA, "imagenes")
PASSWORD_DEMO = "Demo12345"
AHORA = datetime.now(timezone.utc)


def sql(texto):
    return "NULL" if texto is None else "'" + str(texto).replace("'", "''") + "'"


def nsql(texto):
    return "NULL" if texto is None else "N'" + str(texto).replace("'", "''") + "'"


def sin_tildes(texto):
    return "".join(c for c in unicodedata.normalize("NFD", texto) if unicodedata.category(c) != "Mn")


def slug(texto):
    base = sin_tildes(texto).lower()
    resultado, guion = [], True
    for c in base:
        if c.isascii() and c.isalnum():
            resultado.append(c)
            guion = False
        elif not guion:
            resultado.append("-")
            guion = True
    return "".join(resultado).strip("-")


def uuid7(momento):
    ms = int(momento.timestamp() * 1000)
    aleatorio = random.getrandbits(74)
    valor = (ms & ((1 << 48) - 1)) << 80
    valor |= 0x7 << 76
    valor |= ((aleatorio >> 62) & 0xFFF) << 64
    valor |= 0b10 << 62
    valor |= aleatorio & ((1 << 62) - 1)
    return uuid.UUID(int=valor)


def fecha_aleatoria(dias_atras):
    return AHORA - timedelta(days=random.uniform(0, dias_atras), seconds=random.randint(0, 86400))


def precio(minimo, maximo):
    valor = random.uniform(minimo, maximo)
    return int(round(valor / 1000.0)) * 1000 - 100


def hash_password(password):
    salt = os.urandom(16)
    derivado = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, 600000, 32)
    return f"PBKDF2-SHA256$600000${base64.b64encode(salt).decode()}${base64.b64encode(derivado).decode()}"


_CACHE_PNG = {}


def png(color, tamano=400):
    if color in _CACHE_PNG:
        return _CACHE_PNG[color]
    claro = bytes(min(255, int(c + (255 - c) * 0.35)) for c in color)
    oscuro = bytes(color)
    borde = tamano // 5
    fila_borde = bytes([0]) + oscuro * tamano
    fila_centro = bytes([0]) + oscuro * borde + claro * (tamano - 2 * borde) + oscuro * borde
    filas = [fila_centro if borde <= y < tamano - borde else fila_borde for y in range(tamano)]

    def chunk(tipo, datos):
        return struct.pack(">I", len(datos)) + tipo + datos + struct.pack(">I", zlib.crc32(tipo + datos) & 0xFFFFFFFF)

    _CACHE_PNG[color] = (bytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
                         + chunk(b"IHDR", struct.pack(">IIBBBBB", tamano, tamano, 8, 2, 0, 0, 0))
                         + chunk(b"IDAT", zlib.compress(b"".join(filas), 9))
                         + chunk(b"IEND", b""))
    return _CACHE_PNG[color]


# ---------------------------------------------------------------- Auth

NOMBRES = ["Santiago", "Valentina", "Sebastián", "Mariana", "Mateo", "Isabella", "Nicolás", "Daniela", "Samuel",
           "Sofía", "Alejandro", "Gabriela", "Juan Pablo", "Laura", "Andrés", "Camila", "Felipe", "Natalia",
           "David", "Paula", "Julián", "Manuela", "Tomás", "Juliana", "Esteban", "Carolina", "Miguel", "Andrea",
           "Diego", "Luisa", "Simón", "Catalina", "Emiliano", "Antonia", "Martín", "Salomé", "Jerónimo", "Ana María",
           "Cristian", "María José"]
APELLIDOS = ["García", "Rodríguez", "Martínez", "López", "González", "Hernández", "Pérez", "Sánchez", "Ramírez",
             "Torres", "Flórez", "Rivera", "Gómez", "Díaz", "Restrepo", "Vargas", "Castro", "Ortiz", "Rojas",
             "Moreno", "Jiménez", "Muñoz", "Álvarez", "Romero", "Suárez", "Mejía", "Ospina", "Cardona", "Salazar",
             "Zapata", "Arango", "Quintero", "Montoya", "Valencia", "Cárdenas", "Henao", "Giraldo", "Correa",
             "Duque", "Escobar"]

ROLES_NUEVOS = [
    ("CatalogManager", "Gestiona productos, categorías y marcas del catálogo"),
    ("Warehouse", "Personal de bodega e inventario"),
]


def generar_auth():
    lineas = ["SET NOCOUNT ON;", "SET XACT_ABORT ON;", "BEGIN TRANSACTION;", ""]

    for nombre, descripcion in ROLES_NUEVOS:
        lineas.append(
            f"IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE NormalizedName = {nsql(nombre.upper())}) "
            f"INSERT INTO dbo.Roles (Name, NormalizedName, Description) VALUES ({nsql(nombre)}, {nsql(nombre.upper())}, {nsql(descripcion)});")
    lineas.append("")

    plan = ([("admin@ejemplo.com", "Admin", "Demo", ["Administrator"])]
            + [(None, None, None, ["Support"])] * 4
            + [(None, None, None, ["CatalogManager"])] * 4
            + [(None, None, None, ["Warehouse"])] * 4
            + [(None, None, None, ["Customer", "Support"])] * 2
            + [(None, None, None, ["Customer"])] * 185)

    usados = set()
    total = 0
    for i, (email, nombre, apellido, roles) in enumerate(plan, start=1):
        if email is None:
            nombre = random.choice(NOMBRES)
            apellido = random.choice(APELLIDOS)
            base = f"{slug(nombre).replace('-', '')}.{slug(apellido).replace('-', '')}"
            email = f"{base}@ejemplo.com"
            n = 2
            while email in usados:
                email = f"{base}{n}@ejemplo.com"
                n += 1
        usados.add(email)

        creado = fecha_aleatoria(730)
        activo = 0 if (i > 20 and random.random() < 0.05) else 1
        verificado = 1 if (i <= 20 or random.random() < 0.85) else 0
        user_id = uuid.uuid4()
        momento = creado.strftime("%Y-%m-%d %H:%M:%S")

        lineas.append(f"IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE NormalizedEmail = {nsql(email.upper())})")
        lineas.append("BEGIN")
        lineas.append(
            f"    INSERT INTO dbo.Users (UserId, FirstName, LastName, Email, NormalizedEmail, IsEmailVerified, IsActive, CreatedAt, UpdatedAt) "
            f"VALUES ('{user_id}', {nsql(nombre)}, {nsql(apellido)}, {nsql(email)}, {nsql(email.upper())}, {verificado}, {activo}, '{momento}', '{momento}');")
        lineas.append(
            f"    INSERT INTO dbo.UserCredentials (UserId, PasswordHash, PasswordChangedAt) VALUES ('{user_id}', {nsql(hash_password(PASSWORD_DEMO))}, '{momento}');")
        for rol in roles:
            lineas.append(
                f"    INSERT INTO dbo.UserRoles (UserId, RoleId, AssignedAt) SELECT '{user_id}', RoleId, '{momento}' FROM dbo.Roles WHERE NormalizedName = {nsql(rol.upper())};")
        lineas.append("END")
        total += 1

    lineas += ["", "COMMIT;", ""]
    with open(os.path.join(SALIDA, "auth.sql"), "w", encoding="utf-8-sig") as f:
        f.write("\n".join(lineas))
    return total


# ---------------------------------------------------------------- Catálogo

COLORES = {
    "Negro": (33, 33, 33), "Blanco": (236, 236, 236), "Azul marino": (30, 50, 95), "Azul": (52, 101, 164),
    "Gris": (128, 128, 128), "Beige": (214, 196, 160), "Verde oliva": (107, 112, 60), "Rojo": (178, 34, 34),
    "Vino tinto": (114, 30, 50), "Rosado": (232, 150, 170), "Café": (110, 75, 50), "Mostaza": (204, 160, 40),
    "Lila": (180, 150, 200), "Celeste": (135, 190, 225), "Plateado": (190, 195, 200), "Dorado": (205, 170, 90),
    "Grafito": (70, 72, 78), "Terracota": (190, 100, 70), "Verde menta": (150, 210, 180), "Naranja": (235, 120, 40),
}

MARCAS = {
    "moda": ["Andino Wear", "Café Textil", "Ruana & Co", "Urbano Bogotá", "Pacífico Style", "Selva Urbana", "Lino Caribe", "Medellín Denim"],
    "tecnologia": ["Nevado Tech", "Cóndor Digital", "Quantum Andes", "Voltio", "Pixel Sur"],
    "hogar": ["Caribe Home", "Casa Guadua", "Barro & Fuego", "Luz de Montaña", "Hamaca Hogar"],
    "deportes": ["Selva Sport", "Cumbre Running", "Golazo", "Ciclo Andes"],
}
MARCA_INACTIVA = "Hamaca Hogar"

LINEAS = ["Classic", "Urban", "Essential", "Premium", "Andes", "Pacífico", "Caribe", "Sabana", "Origen", "Nativa", "Brisa", "Montaña"]

TALLAS_LETRA = ["XS", "S", "M", "L", "XL", "XXL"]
TALLAS_INFANTIL = ["2", "4", "6", "8", "10", "12"]
TALLAS_BEBE = ["0-3M", "3-6M", "6-12M", "12-18M"]

ATRIBUTOS = {
    "ropa": [("material", ["Algodón 100%", "Algodón y poliéster", "Lino", "Viscosa", "Poliéster reciclado"]),
             ("cuidado", ["Lavar a máquina en frío", "Lavar a mano", "No usar secadora"]),
             ("origen", ["Colombia", "Perú", "México"])],
    "calzado": [("material", ["Cuero", "Cuero sintético", "Lona", "Gamuza", "Malla transpirable"]),
                ("suela", ["Caucho", "EVA", "Goma antideslizante"]),
                ("origen", ["Colombia", "Brasil", "Vietnam"])],
    "tecnologia": [("garantia", ["6 meses", "1 año", "2 años"]),
                   ("conectividad", ["Bluetooth 5.3", "Wi-Fi 6", "USB-C", "5G"]),
                   ("origen", ["China", "Corea del Sur", "Taiwán"])],
    "hogar": [("material", ["Cerámica", "Acero inoxidable", "Madera de pino", "Algodón percal", "Vidrio templado"]),
              ("garantia", ["3 meses", "6 meses", "1 año"]),
              ("origen", ["Colombia", "China", "México"])],
    "accesorio": [("material", ["Cuero", "Lona", "Metal", "Acetato", "Tela"]),
                  ("origen", ["Colombia", "China", "Italia"])],
}

# (nombre, slug, [subcategorías]); cada subcategoría: (nombre, slug, grupo de marca, tipo, rango de precios, productos base)
CATEGORIAS = [
    ("Hombre", "hombre", [
        ("Camisas", "camisas-hombre", "moda", "letra", (89900, 189900), ["Camisa Oxford", "Camisa de lino", "Camisa a cuadros", "Camisa manga corta", "Camisa slim fit", "Camisa de jean"]),
        ("Camisetas", "camisetas-hombre", "moda", "letra", (39900, 89900), ["Camiseta básica", "Camiseta estampada", "Camiseta polo", "Camiseta cuello V", "Camiseta oversize"]),
        ("Pantalones", "pantalones-hombre", "moda", "cintura", (99900, 199900), ["Pantalón chino", "Pantalón cargo", "Pantalón de vestir", "Jogger"]),
        ("Jeans", "jeans-hombre", "moda", "cintura", (119900, 229900), ["Jean slim", "Jean recto", "Jean skinny", "Jean relaxed"]),
        ("Chaquetas", "chaquetas-hombre", "moda", "letra", (149900, 349900), ["Chaqueta de jean", "Chaqueta impermeable", "Chaqueta bomber", "Buzo con capucha"]),
        ("Calzado", "calzado-hombre", "moda", "pie-hombre", (129900, 299900), ["Tenis urbanos", "Zapatos de cuero", "Botas", "Mocasines"]),
        ("Accesorios", "accesorios-hombre", "moda", "color", (29900, 119900), ["Cinturón de cuero", "Gorra", "Billetera", "Gafas de sol"]),
    ]),
    ("Mujer", "mujer", [
        ("Blusas", "blusas-mujer", "moda", "letra", (59900, 139900), ["Blusa de seda", "Blusa campesina", "Blusa manga larga", "Blusa estampada", "Top básico"]),
        ("Vestidos", "vestidos-mujer", "moda", "letra", (99900, 259900), ["Vestido midi", "Vestido largo", "Vestido camisero", "Vestido de fiesta", "Vestido floral"]),
        ("Faldas", "faldas-mujer", "moda", "letra", (69900, 149900), ["Falda plisada", "Falda midi", "Minifalda de jean", "Falda lápiz"]),
        ("Pantalones", "pantalones-mujer", "moda", "letra", (79900, 169900), ["Pantalón palazzo", "Pantalón de vestir", "Legging", "Pantalón culotte"]),
        ("Jeans", "jeans-mujer", "moda", "talla-mujer", (109900, 209900), ["Jean mom", "Jean skinny", "Jean wide leg", "Jean tiro alto"]),
        ("Chaquetas", "chaquetas-mujer", "moda", "letra", (129900, 299900), ["Chaqueta de jean", "Blazer", "Chaqueta acolchada", "Cárdigan", "Gabardina"]),
        ("Calzado", "calzado-mujer", "moda", "pie-mujer", (99900, 249900), ["Tenis urbanos", "Tacones", "Botines", "Sandalias", "Baletas"]),
        ("Bolsos", "bolsos-mujer", "moda", "color", (79900, 249900), ["Bolso de mano", "Mochila", "Bolso cruzado", "Cartera", "Tote bag"]),
    ]),
    ("Niños", "ninos", [
        ("Niño", "nino", "moda", "infantil", (29900, 79900), ["Camiseta infantil", "Pantalón infantil", "Sudadera infantil", "Pijama infantil"]),
        ("Niña", "nina", "moda", "infantil", (29900, 89900), ["Vestido infantil", "Leggings infantil", "Blusa infantil", "Conjunto infantil"]),
        ("Bebé", "bebe", "moda", "bebe", (19900, 59900), ["Body de algodón", "Mameluco", "Conjunto de bebé", "Gorro de bebé"]),
    ]),
    ("Tecnología", "tecnologia", [
        ("Celulares", "celulares", "tecnologia", "almacenamiento", (699900, 2499900), ["Smartphone Nova", "Smartphone Pulse", "Smartphone Vértice", "Smartphone Lite"]),
        ("Portátiles", "portatiles", "tecnologia", "memoria", (1899900, 4999900), ["Portátil 14 pulgadas", "Portátil 15 pulgadas", "Ultrabook", "Portátil gamer"]),
        ("Audífonos", "audifonos", "tecnologia", "color", (89900, 899900), ["Audífonos inalámbricos", "Audífonos de diadema", "Audífonos deportivos", "Audífonos con cancelación de ruido"]),
        ("Relojes inteligentes", "relojes-inteligentes", "tecnologia", "reloj", (199900, 1499900), ["Reloj inteligente", "Banda deportiva", "Reloj inteligente GPS"]),
        ("Accesorios de tecnología", "accesorios-tecnologia", "tecnologia", "color", (29900, 349900), ["Cargador rápido 30W", "Power bank 10000 mAh", "Mouse inalámbrico", "Teclado mecánico", "Soporte para portátil"]),
    ]),
    ("Hogar", "hogar", [
        ("Cocina", "cocina", "hogar", "tamano-cocina", (49900, 599900), ["Sartén antiadherente", "Juego de ollas", "Licuadora", "Cafetera", "Set de cuchillos"]),
        ("Decoración", "decoracion", "hogar", "color", (29900, 249900), ["Cojín decorativo", "Florero de cerámica", "Cuadro decorativo", "Espejo redondo", "Vela aromática"]),
        ("Ropa de cama", "ropa-de-cama", "hogar", "cama", (59900, 399900), ["Juego de sábanas", "Edredón", "Almohada", "Cobija"]),
        ("Iluminación", "iluminacion", "hogar", "color", (39900, 449900), ["Lámpara de mesa", "Lámpara de pie", "Bombillo LED inteligente", "Tira LED"]),
    ]),
    ("Deportes", "deportes", [
        ("Ropa deportiva", "ropa-deportiva", "deportes", "letra", (49900, 179900), ["Camiseta deportiva", "Pantaloneta", "Licra deportiva", "Chaqueta rompevientos", "Top deportivo"]),
        ("Calzado deportivo", "calzado-deportivo", "deportes", "pie-hombre", (149900, 449900), ["Tenis para correr", "Guayos", "Tenis de entrenamiento", "Zapatillas de fútbol sala"]),
        ("Implementos", "implementos-deportivos", "deportes", "color", (29900, 299900), ["Balón de fútbol", "Mancuernas", "Colchoneta de yoga", "Bandas elásticas", "Termo deportivo"]),
    ]),
]

TIPO_ATRIBUTOS = {"tecnologia": "tecnologia", "hogar": "hogar"}


def opciones_variantes(tipo, colores):
    if tipo == "letra":
        inicio = random.randint(0, 2)
        tallas = TALLAS_LETRA[inicio:inicio + random.randint(3, 4)]
        return [({"color": c, "talla": t}, 0) for c in colores for t in tallas]
    if tipo == "cintura":
        tallas = [str(t) for t in range(28, 40, 2)][random.randint(0, 1):][:random.randint(4, 5)]
        return [({"color": c, "talla": t}, 0) for c in colores for t in tallas]
    if tipo == "talla-mujer":
        tallas = [str(t) for t in range(4, 18, 2)][random.randint(0, 2):][:4]
        return [({"color": c, "talla": t}, 0) for c in colores for t in tallas]
    if tipo == "pie-hombre":
        return [({"color": c, "talla": str(t)}, 0) for c in colores for t in range(38, 44)]
    if tipo == "pie-mujer":
        return [({"color": c, "talla": str(t)}, 0) for c in colores for t in range(35, 40)]
    if tipo == "infantil":
        return [({"color": c, "talla": t}, 0) for c in colores for t in TALLAS_INFANTIL[random.randint(0, 2):][:4]]
    if tipo == "bebe":
        return [({"color": c, "talla": t}, 0) for c in colores for t in TALLAS_BEBE]
    if tipo == "almacenamiento":
        return [({"color": c, "almacenamiento": a}, extra) for c in colores
                for a, extra in [("128 GB", 0), ("256 GB", 300000), ("512 GB", 800000)]]
    if tipo == "memoria":
        return [({"memoria": m, "almacenamiento": a}, extra) for m, a, extra in
                [("8 GB", "512 GB", 0), ("16 GB", "512 GB", 600000), ("16 GB", "1 TB", 1000000), ("32 GB", "1 TB", 1900000)]]
    if tipo == "reloj":
        return [({"color": c, "tamano": t}, extra) for c in colores for t, extra in [("41 mm", 0), ("45 mm", 100000)]]
    if tipo == "tamano-cocina":
        return [({"tamano": t}, extra) for t, extra in [("Pequeño", 0), ("Mediano", 40000), ("Grande", 90000)]]
    if tipo == "cama":
        return [({"tamano": t, "color": c}, extra) for c in colores
                for t, extra in [("Sencilla", 0), ("Doble", 40000), ("Queen", 80000), ("King", 120000)]]
    return [({"color": c}, 0) for c in colores]


def descripcion(nombre, marca, subcategoria, atributos):
    detalle = ", ".join(f"{k}: {v.lower()}" for k, v in atributos.items())
    frases = [
        f"{nombre} de {marca}, pensado para el uso diario.",
        f"Diseño versátil de la línea {subcategoria.lower()} con acabados de calidad.",
        f"Características: {detalle}.",
        random.choice(["Ideal para regalar.", "Edición disponible por tiempo limitado.", "Uno de los más vendidos de la temporada.", "Fabricado con procesos responsables."]),
    ]
    return " ".join(frases)


def generar_catalogo():
    os.makedirs(CARPETA_IMAGENES, exist_ok=True)
    lineas = ["BEGIN;", "SET LOCAL search_path = catalogo;", ""]
    resumen = {"categorias": 0, "marcas": 0, "productos": 0, "variantes": 0, "imagenes": 0}

    for orden, (nombre, slug_padre, hijas) in enumerate(CATEGORIAS, start=1):
        lineas.append(
            f"INSERT INTO catalogo.categorias (nombre, slug, descripcion, orden) VALUES ({sql(nombre)}, {sql(slug_padre)}, "
            f"{sql('Productos de ' + nombre.lower())}, {orden}) ON CONFLICT (slug) DO NOTHING;")
        resumen["categorias"] += 1
        for orden_hija, hija in enumerate(hijas, start=1):
            lineas.append(
                f"INSERT INTO catalogo.categorias (categoria_padre_id, nombre, slug, descripcion, orden) "
                f"SELECT id, {sql(hija[0])}, {sql(hija[1])}, {sql(hija[0] + ' para ' + nombre.lower())}, {orden_hija} "
                f"FROM catalogo.categorias WHERE slug = {sql(slug_padre)} ON CONFLICT (slug) DO NOTHING;")
            resumen["categorias"] += 1
    lineas.append("")

    for grupo, marcas in MARCAS.items():
        for marca in marcas:
            activa = "false" if marca == MARCA_INACTIVA else "true"
            lineas.append(
                f"INSERT INTO catalogo.marcas (nombre, slug, activa) VALUES ({sql(marca)}, {sql(slug(marca))}, {activa}) ON CONFLICT (slug) DO NOTHING;")
            resumen["marcas"] += 1
    lineas.append("")

    nombres_usados = set()
    contador_sku = 0

    for _, _, hijas in CATEGORIAS:
        for nombre_sub, slug_sub, grupo, tipo, (pmin, pmax), bases in hijas:
            for base in bases:
                for linea in random.sample(LINEAS, random.randint(2, 4)):
                    marca = random.choice(MARCAS[grupo])
                    nombre = f"{base} {linea}"
                    if nombre in nombres_usados:
                        nombre = f"{base} {linea} {marca}"
                    if nombre in nombres_usados:
                        continue
                    nombres_usados.add(nombre)

                    creado = fecha_aleatoria(365)
                    producto_id = uuid7(creado)
                    precio_base = precio(pmin, pmax)
                    plantilla = ATRIBUTOS[TIPO_ATRIBUTOS.get(grupo, "calzado" if tipo.startswith("pie") else "accesorio" if tipo == "color" and grupo == "moda" else "ropa")]
                    atributos = {k: random.choice(v) for k, v in plantilla}
                    activo = "false" if random.random() < 0.04 else "true"
                    destacado = "true" if random.random() < 0.08 else "false"
                    descuento = random.random() < 0.2
                    colores = random.sample(list(COLORES), random.randint(1, 3) if tipo not in ("memoria", "tamano-cocina") else 1)
                    atributos_sql = sql(json.dumps(atributos, ensure_ascii=False)) + "::jsonb"

                    lineas.append(
                        "INSERT INTO catalogo.productos (id, categoria_id, marca_id, nombre, slug, descripcion_corta, descripcion, "
                        "precio_base, atributos, activo, destacado, creado_en, actualizado_en) "
                        f"SELECT '{producto_id}', c.id, m.id, {sql(nombre)}, {sql(slug(nombre))}, "
                        f"{sql(base + ' ' + marca)}, {sql(descripcion(nombre, marca, nombre_sub, atributos))}, "
                        f"{precio_base}, {atributos_sql}, {activo}, {destacado}, '{creado.isoformat()}', '{creado.isoformat()}' "
                        f"FROM catalogo.categorias c, catalogo.marcas m WHERE c.slug = {sql(slug_sub)} AND m.slug = {sql(slug(marca))};")
                    resumen["productos"] += 1

                    primera_por_color = {}
                    for opciones, extra in opciones_variantes(tipo, colores):
                        contador_sku += 1
                        variante_id = uuid7(creado + timedelta(milliseconds=contador_sku % 1000))
                        codigo = "-".join(slug(str(v)).upper().replace("-", "")[:4] for v in opciones.values())
                        sku = f"P{contador_sku:05d}-{codigo}"[:50]
                        precio_variante = precio_base + extra
                        if descuento:
                            precio_antes = precio_variante
                            precio_variante = int(round(precio_variante * random.choice([0.7, 0.8, 0.85, 0.9]) / 1000.0)) * 1000 - 100
                        else:
                            precio_antes = None
                        propio = precio_variante if (precio_variante != precio_base) else None
                        if propio is None and precio_antes is not None:
                            precio_antes = None
                        activa = "false" if random.random() < 0.03 else "true"
                        opciones_sql = sql(json.dumps(opciones, ensure_ascii=False)) + "::jsonb"
                        lineas.append(
                            "INSERT INTO catalogo.producto_variantes (id, producto_id, sku, opciones, precio, precio_antes, activa, creado_en, actualizado_en) "
                            f"VALUES ('{variante_id}', '{producto_id}', {sql(sku)}, {opciones_sql}, "
                            f"{propio if propio is not None else 'NULL'}, {precio_antes if precio_antes is not None else 'NULL'}, "
                            f"{activa}, '{creado.isoformat()}', '{creado.isoformat()}');")
                        resumen["variantes"] += 1
                        color = opciones.get("color")
                        if color and color not in primera_por_color:
                            primera_por_color[color] = variante_id

                    colores_imagen = list(primera_por_color.items()) or [(colores[0], None)]
                    for orden_img, (color, variante_id) in enumerate(colores_imagen):
                        ruta = f"productos/{producto_id.hex}/{uuid.uuid4().hex}.png"
                        destino = os.path.join(CARPETA_IMAGENES, *ruta.split("/"))
                        os.makedirs(os.path.dirname(destino), exist_ok=True)
                        with open(destino, "wb") as f:
                            f.write(png(COLORES[color]))
                        variante_sql = f"'{variante_id}'" if variante_id else "NULL"
                        lineas.append(
                            "INSERT INTO catalogo.producto_imagenes (producto_id, variante_id, ruta_archivo, texto_alternativo, orden, es_principal) "
                            f"VALUES ('{producto_id}', {variante_sql}, {sql(ruta)}, {sql(nombre + ' color ' + color.lower())}, "
                            f"{orden_img}, {'true' if orden_img == 0 else 'false'});")
                        resumen["imagenes"] += 1
                    lineas.append("")

    lineas += ["COMMIT;", ""]
    with open(os.path.join(SALIDA, "catalogo.sql"), "w", encoding="utf-8") as f:
        f.write("\n".join(lineas))
    return resumen


if __name__ == "__main__":
    os.makedirs(SALIDA, exist_ok=True)
    inicio = time.time()
    print("Catálogo:", generar_catalogo())
    print("Usuarios:", generar_auth())
    print(f"Listo en {time.time() - inicio:.0f} s. Contraseña de todos los usuarios de ejemplo: {PASSWORD_DEMO}")
