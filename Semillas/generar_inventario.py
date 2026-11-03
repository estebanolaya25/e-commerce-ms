"""Genera el stock inicial de Inventario a partir de las variantes que ya existen en Catálogo.

Uso:  python generar_inventario.py
Lee las variantes con psql (usuario catalog_app, contraseña desde ../.env) y escribe salida/inventario.sql.
"""
import csv
import io
import os
import random
import subprocess

random.seed(2027)

RAIZ = os.path.dirname(os.path.abspath(__file__))
SALIDA = os.path.join(RAIZ, "salida")
PSQL = r"C:\Program Files\PostgreSQL\18\bin\psql.exe"
LOTE = 500


def leer_env():
    with open(os.path.join(RAIZ, "..", ".env"), encoding="utf-8") as f:
        return dict(linea.strip().split("=", 1) for linea in f if "=" in linea)


def leer_variantes():
    entorno = dict(os.environ, PGPASSWORD=leer_env()["CATALOG_DB_PASSWORD"], PGCLIENTENCODING="UTF8")
    consulta = "COPY (SELECT sku, producto_id, id FROM catalogo.producto_variantes ORDER BY sku) TO STDOUT WITH CSV"
    salida = subprocess.run([PSQL, "-U", "catalog_app", "-h", "localhost", "-d", "ecommerce_catalog", "-c", consulta],
                            env=entorno, capture_output=True, text=True, encoding="utf-8", check=True).stdout
    return list(csv.reader(io.StringIO(salida)))


def cantidad_aleatoria():
    r = random.random()
    if r < 0.10:
        return 0
    if r < 0.25:
        return random.randint(1, 4)
    return random.randint(5, 80)


def sql(texto):
    return "N'" + texto.replace("'", "''") + "'"


def generar():
    variantes = leer_variantes()
    lineas = ["SET NOCOUNT ON;", "SET XACT_ABORT ON;", "BEGIN TRANSACTION;", ""]
    resumen = {"skus": 0, "agotados": 0, "pocas_unidades": 0, "unidades": 0}

    filas_stock, filas_mov = [], []
    for sku, producto_id, variante_id in variantes:
        cantidad = cantidad_aleatoria()
        filas_stock.append(f"({sql(sku)}, '{producto_id}', '{variante_id}', {cantidad}, 5)")
        filas_mov.append(f"({sql(sku)}, 'Alta', 0, 0, 0, N'Carga inicial')")
        if cantidad > 0:
            filas_mov.append(f"({sql(sku)}, 'Entrada', {cantidad}, {cantidad}, 0, N'Inventario inicial')")
        resumen["skus"] += 1
        resumen["unidades"] += cantidad
        resumen["agotados"] += cantidad == 0
        resumen["pocas_unidades"] += 0 < cantidad <= 5

    for i in range(0, len(filas_stock), LOTE):
        lineas.append("INSERT INTO inv.Stock (Sku, ProductoId, VarianteId, CantidadFisica, StockMinimo) VALUES")
        lineas.append(",\n".join(filas_stock[i:i + LOTE]) + ";")
    for i in range(0, len(filas_mov), LOTE):
        lineas.append("INSERT INTO inv.Movimientos (Sku, Tipo, Cantidad, FisicaResultante, ReservadaResultante, Motivo) VALUES")
        lineas.append(",\n".join(filas_mov[i:i + LOTE]) + ";")

    lineas += ["", "COMMIT;", ""]
    os.makedirs(SALIDA, exist_ok=True)
    with open(os.path.join(SALIDA, "inventario.sql"), "w", encoding="utf-8-sig") as f:
        f.write("\n".join(lineas))
    return resumen


if __name__ == "__main__":
    print("Inventario:", generar())
