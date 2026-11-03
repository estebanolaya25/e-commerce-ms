param(
    [switch]$SinDatosPrueba
)

$ErrorActionPreference = 'Stop'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$carpeta = $PSScriptRoot
$archivoEnv = Join-Path $carpeta '..\..\.env'

$claveApp = (Get-Content $archivoEnv | Where-Object { $_ -match '^CATALOG_DB_PASSWORD=' }) -replace '^CATALOG_DB_PASSWORD=', ''
if ([string]::IsNullOrWhiteSpace($claveApp)) {
    throw "Falta CATALOG_DB_PASSWORD en $archivoEnv"
}

$segura = Read-Host 'Contraseña del usuario postgres' -AsSecureString
$env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura))

function Ejecutar([string]$baseDatos, [string]$script, [string[]]$extra = @()) {
    Write-Host "-> $script" -ForegroundColor Cyan
    & $psql -U postgres -h localhost -d $baseDatos -v ON_ERROR_STOP=1 -q @extra -f (Join-Path $carpeta $script)
    if ($LASTEXITCODE -ne 0) { throw "Falló $script" }
}

try {
    Ejecutar 'postgres' '00_crear_base_y_usuario.sql' @('-v', "clave_app=$claveApp")
    Ejecutar 'ecommerce_catalog' '01_esquema.sql'
    Ejecutar 'ecommerce_catalog' '02_permisos.sql'
    if (-not $SinDatosPrueba) {
        Ejecutar 'ecommerce_catalog' '03_datos_prueba.sql'
    }
    Write-Host 'Base ecommerce_catalog creada correctamente.' -ForegroundColor Green
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
