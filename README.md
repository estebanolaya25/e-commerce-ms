# E-Commerce con microservicios

Tienda en línea de ropa, calzado y accesorios construida con **arquitectura de microservicios** en **.NET 9**, con frontend en **Angular 21**. Cada parte del negocio vive en un servicio independiente con su propia base de datos. Los servicios se comunican por HTTP cuando necesitan una respuesta inmediata, y por eventos en **RabbitMQ** cuando solo deben avisar que algo pasó.

> La app móvil (Flutter) consume esta misma API y está en su propio repositorio: [AppE-Commerce_Flutter_Dart](https://github.com/estebanolaya25/AppE-Commerce_Flutter_Dart).

## Funcionalidades

**Cliente**
- Catálogo con filtros por categoría, marca y precio, y búsqueda de texto libre.
- Detalle de producto con selección de variante (talla y color) y disponibilidad en tiempo real.
- Registro, inicio de sesión y renovación automática de la sesión.
- Carrito, checkout con **reserva de stock por 15 minutos** y pago con tarjeta (pasarela simulada).
- Historial de pedidos con línea de tiempo de estados y cancelación mientras no estén pagados.
- Asistente virtual (Andi) en un chat flotante: responde preguntas de envíos, pagos, pedidos, devoluciones, tallas y cuenta con RAG sobre una base de conocimiento propia.

**Panel interno (`/admin`) por roles**

| Rol | Qué puede hacer |
|---|---|
| Administrador | Todo, incluida la gestión de usuarios y roles |
| Soporte | Consultar pedidos y pagos (solo lectura) |
| Bodega | Despachar pedidos pagados y gestionar el inventario |
| Gestor de catálogo | Productos, variantes, imágenes, categorías y marcas |

## Arquitectura

```
   Angular :4300 ──► GATEWAY (YARP) :5100   valida JWT, rate limiting, enrutamiento
                          │
     ┌──────────┬─────────┼──────────┬──────────┬──────────┐
     ▼          ▼         ▼          ▼          ▼          ▼
   AUTH     CATÁLOGO  INVENTARIO  CARRITO    PEDIDOS     PAGOS
   :5101     :5102      :5103      :5104      :5105      :5106
     │       │    │       │          │          │          │
  SQL Server Postgres Redis SQL Server Redis  SQL Server  SQL Server

   ASISTENTE :5107 ──► Ollama (host, :11434)  +  ChromaDB (:8000)

   ══════════════════════ RabbitMQ (eventos) ══════════════════════
   Catálogo ──VarianteCreada──────────────────────────► Inventario
   Pedidos  ──PedidoCreado────────────────────────────► Carrito
   Pagos    ──PagoAprobado / PagoRechazado / Reembolsado► Pedidos
```

| Servicio | Responsabilidad | Almacenamiento |
|---|---|---|
| **Gateway** | Punto único de entrada, validación de JWT, rate limiting por ruta | — |
| **Auth** | Registro, login, JWT + refresh token con rotación, usuarios y roles | SQL Server |
| **Catálogo** | Productos, variantes, imágenes, categorías y marcas | PostgreSQL + caché en Redis |
| **Inventario** | Stock, reservas con expiración y movimientos | SQL Server |
| **Carrito** | Carrito por usuario con precios y disponibilidad | Redis |
| **Pedidos** | Checkout y orquestación de la saga del pedido | SQL Server |
| **Pagos** | Pasarela de pagos simulada | SQL Server |
| **Asistente** | Chat con RAG: busca en la base de conocimiento y responde con un modelo local | ChromaDB (vectores) |

### Decisiones de diseño

- **Una base de datos por servicio.** Ningún servicio lee las tablas de otro.
- **Saga orquestada** en el checkout: Pedidos reserva stock en Inventario, inicia el pago y confirma o compensa según el resultado.
- **Patrón Outbox** en Pedidos y Pagos: el evento se guarda en la misma transacción que el cambio de estado. Si RabbitMQ se cae, el evento se publica cuando vuelva y no se pierde.
- **Consumidores idempotentes**: la entrega es "al menos una vez", así que los mensajes duplicados se detectan y descartan.
- **Idempotency-Key** en el checkout para que un doble clic no cree dos pedidos.
- **Resiliencia HTTP** con reintentos, timeouts y circuit breaker (Polly).
- **Endpoints internos** protegidos con `X-Api-Key`, que no se exponen a través del gateway.
- **ADO.NET puro** (`SqlCommand`, `NpgsqlCommand`), sin Entity Framework, y arquitectura en capas en cada servicio (API → Negocio → Datos → DTO).

## Tecnologías

| Capa | Tecnología |
|---|---|
| Backend | .NET 9, ASP.NET Core Web API, C# |
| Gateway | YARP |
| Mensajería | RabbitMQ + MassTransit |
| Bases de datos | SQL Server, PostgreSQL |
| Caché | Redis |
| Seguridad | JWT, refresh tokens con rotación, PBKDF2 para contraseñas |
| Frontend | Angular 21 (NgModules), RxJS, Bootstrap 5, SCSS |
| IA | Ollama (`qwen3.5`, `nomic-embed-text`), ChromaDB |
| Contenedores | Docker Compose |

## Estructura del repositorio

```
Auth_BACK/         Autenticación y usuarios
Catalog_BACK/      Catálogo de productos
Inventory_BACK/    Inventario y reservas
Cart_BACK/         Carrito
Orders_BACK/       Pedidos (orquestador)
Payments_BACK/     Pagos simulados
Assistant_BACK/    Asistente virtual (RAG con Ollama y ChromaDB)
Gateway_BACK/      API Gateway (YARP)
Ecommerce_FRONT/   Frontend Angular
Postman/           Colección de pruebas de la API
Semillas/          Generadores de datos de ejemplo
```

Cada servicio sigue la misma estructura de capas: `*_Api`, `CapaNegocios`, `CapaDatos`, `CapaDTO` y, cuando aplica, `BaseDatos/` con los scripts SQL. El asistente es la excepción: no tiene base de datos relacional y es un solo proyecto (`Assistant_Api`).

## Cómo ejecutarlo

### Requisitos

- .NET 9 SDK, Node.js y Angular CLI
- SQL Server, PostgreSQL, Redis y RabbitMQ
- Python 3 (para generar los datos de ejemplo)
- Docker Desktop (opcional)
- [Ollama](https://ollama.com) para el asistente, con los modelos `ollama pull qwen3.5` y `ollama pull nomic-embed-text`

### 1. Configuración

Las contraseñas y claves **no están en el repositorio**. Copia los archivos de ejemplo y completa los valores:

```powershell
Copy-Item .env.example .env
# Para correr en local, en cada servicio:
Copy-Item <Servicio>_BACK\<Servicio>_Api\appsettings.json.example <Servicio>_BACK\<Servicio>_Api\appsettings.json
```

La clave JWT (`JWT_KEY`) debe ser la misma en Auth, el gateway y los demás servicios.

### 2. Bases de datos

Auth, Catálogo, Inventario, Pedidos y Pagos tienen en `BaseDatos/` scripts numerados que se ejecutan en orden (Carrito usa solo Redis y el gateway no tiene base de datos):

1. `00_crear_base_y_login.sql`: crea la base y un usuario propio del servicio. En Catálogo (PostgreSQL) es `00_crear_base_y_usuario.sql`, y `crear_base.ps1` ejecuta todo el proceso.
2. `01_esquema.sql`: tablas, índices y restricciones.
3. `02_permisos.sql`: permisos mínimos para ese usuario (nunca `sa` ni dueño de la base).

Los datos de ejemplo se generan con `Semillas/generar_semillas.py` y `Semillas/generar_inventario.py`. Crean un administrador, personal de cada rol, más de 200 clientes y el catálogo con imágenes.

### 3. Levantar las APIs

**Con Docker:**

```bash
docker compose up -d --build
```

**En local, sin Docker** (abre una ventana por servicio):

```powershell
powershell -ExecutionPolicy Bypass -File .\iniciar-local.ps1
```

### 4. Frontend

```bash
cd Ecommerce_FRONT
npm install
npx ng serve --port 4300
```

Abre `http://localhost:4300`.

### 5. Asistente virtual

Ollama corre en el equipo (fuera de Docker) para usar la GPU. Con Docker, `chroma` y `assistant-api` se levantan con el resto; en local, el asistente busca ChromaDB en `http://localhost:8000`.

- Al arrancar, si la colección está vacía, indexa sola los documentos de `Assistant_BACK/Assistant_Api/BaseConocimiento/`.
- Tras cambiar los documentos: `POST /api/asistente/documentos/reindexar` con un token de administrador.
- El prompt, el modelo, los sinónimos y los límites están en `Assistant_BACK/Assistant_Api/asistente.json`.
- Seguridad: el modelo se fija en el servidor, solo se aceptan mensajes `user`/`assistant` (un cliente no puede inyectar `system`), el historial se recorta, el gateway limita a 10 preguntas por minuto y como máximo 2 respuestas se generan a la vez (las demás esperan turno o reciben 503).
- Los documentos marcados con `BORRADOR` (devoluciones, tiempos de entrega, contacto, medidas de tallas) deben revisarse con el negocio.

### Probar la API

- **Swagger:** `http://localhost:5100/swagger` (Auth) y `http://localhost:5100/api/<servicio>/swagger`
- **Postman:** importa `Postman/E-Commerce-ms.postman_collection.json`
- **Tarjetas de prueba:** `4111 1111 1111 1111` (aprueba) y `4000 0000 0000 0002` (rechaza)
