# Arranca los 8 microservicios en local (fuera de Docker), cada uno en su propia ventana.
# Uso:  powershell -ExecutionPolicy Bypass -File .\iniciar-local.ps1
# Para detener: cerrar las ventanas o Ctrl+C en cada una.

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot

$servicios = @(
    @{ Nombre = 'Auth';       Proyecto = 'Auth_BACK\Auth_Api';           Puerto = 5101 },
    @{ Nombre = 'Catalogo';   Proyecto = 'Catalog_BACK\Catalog_Api';     Puerto = 5102 },
    @{ Nombre = 'Inventario'; Proyecto = 'Inventory_BACK\Inventory_Api'; Puerto = 5103 },
    @{ Nombre = 'Carrito';    Proyecto = 'Cart_BACK\Cart_Api';           Puerto = 5104 },
    @{ Nombre = 'Pedidos';    Proyecto = 'Orders_BACK\Orders_Api';       Puerto = 5105 },
    @{ Nombre = 'Pagos';      Proyecto = 'Payments_BACK\Payments_Api';   Puerto = 5106 },
    @{ Nombre = 'Asistente';  Proyecto = 'Assistant_BACK\Assistant_Api'; Puerto = 5107 },
    @{ Nombre = 'Gateway';    Proyecto = 'Gateway_BACK\Gateway_Api';     Puerto = 5100 }
)

# Si las APIs siguen corriendo en Docker, ocuparían el puerto 5100 y competirían por los mensajes de RabbitMQ.
$enDocker = docker ps --filter "label=com.docker.compose.project=ecommerce" --format "{{.Names}}" 2>$null
if ($enDocker) {
    Write-Host "Las APIs siguen corriendo en Docker: $($enDocker -join ', ')" -ForegroundColor Yellow
    Write-Host "Deténgalas primero con:  docker compose stop" -ForegroundColor Yellow
    exit 1
}

# El asistente necesita Ollama (:11434) y ChromaDB (:8000). Si faltan, arranca igual pero responde 502.
foreach ($dependencia in @(@{ Nombre = 'Ollama'; Url = 'http://localhost:11434/api/tags' },
                           @{ Nombre = 'ChromaDB'; Url = 'http://localhost:8000/api/v2/heartbeat' })) {
    try { Invoke-WebRequest $dependencia.Url -UseBasicParsing -TimeoutSec 2 | Out-Null }
    catch { Write-Host "Aviso: $($dependencia.Nombre) no responde en $($dependencia.Url); el asistente no podrá contestar." -ForegroundColor Yellow }
}

Write-Host 'Compilando la solución...' -ForegroundColor Cyan
dotnet build (Join-Path $raiz 'E-Commerce-ms.sln') -v q --nologo
if ($LASTEXITCODE -ne 0) { throw 'La compilación falló.' }

foreach ($s in $servicios) {
    $ruta = Join-Path $raiz $s.Proyecto
    $comando = "`$Host.UI.RawUI.WindowTitle = '$($s.Nombre) :$($s.Puerto)'; dotnet run --no-build --launch-profile http --project '$ruta'"
    Start-Process powershell -ArgumentList '-NoExit', '-Command', $comando
    Write-Host ("  {0,-11} http://localhost:{1}" -f $s.Nombre, $s.Puerto)
}

Write-Host ''
Write-Host 'Todo arrancando. En unos segundos:' -ForegroundColor Green
Write-Host '  Gateway:  http://localhost:5100/health'
Write-Host '  Swagger:  http://localhost:5100/swagger  (Auth)  y  http://localhost:5100/api/<servicio>/swagger'
