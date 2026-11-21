<#
  Ejecución completa de la demostración (Windows):
    1. Crea SitioWebDB (origen) y OpinionesStagingDB (staging) con sqlcmd
    2. Compila la solución
    3. Inicia la API de redes sociales y ejecuta el Worker de extracción
       (la API key de desarrollo llega por variable de entorno desde launchSettings.json)
    4. Muestra las consultas de validación del staging
  Todo lo que sale en pantalla queda guardado en evidencias\ejecucion_AAAAMMDD_HHMMSS.txt

  Uso: clic derecho > Ejecutar con PowerShell, o doble clic en scripts\demo-completa.bat
  Parámetro opcional: -Servidor ".\SQLEXPRESS"
#>
param([string]$Servidor = ".\SQLEXPRESS")

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

New-Item -ItemType Directory -Force -Path "$raiz\evidencias" | Out-Null
$transcripcion = "$raiz\evidencias\ejecucion_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"
Start-Transcript -Path $transcripcion | Out-Null

function Paso($texto) { Write-Host ""; Write-Host "=== $texto ===" -ForegroundColor Cyan }

$api = $null
try {
    Paso "1. Bases de datos en $Servidor"
    foreach ($script in "00_CrearSitioWebDB.sql", "01_CrearStagingDB.sql") {
        Write-Host "Ejecutando database\$script"
        sqlcmd -S $Servidor -E -b -f 65001 -i "$raiz\database\$script"
        if ($LASTEXITCODE -ne 0) { throw "Falló $script" }
    }

    Paso "2. Compilación"
    dotnet build "$raiz\OpinionesClientes.Extraccion.sln" -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "La compilación falló" }

    Paso "3. API de redes sociales (http://localhost:5080)"
    $api = Start-Process dotnet -ArgumentList "run --no-build --project `"$raiz\src\OpinionesClientes.SocialApi`"" `
        -WindowStyle Minimized -PassThru
    for ($i = 0; $i -lt 40; $i++) {
        try { Invoke-RestMethod http://localhost:5080/health -TimeoutSec 2 | Out-Null; break } catch { Start-Sleep -Milliseconds 500 }
    }
    Write-Host "API lista. Prueba sin API key:"
    try { Invoke-RestMethod http://localhost:5080/api/v1/comentarios | Out-Null } catch { Write-Host "  -> $($_.Exception.Message)" -ForegroundColor Yellow }

    Paso "4. Worker Service de extracción"
    dotnet run --no-build --project "$raiz\src\OpinionesClientes.Worker"

    Paso "5. Validación en OpinionesStagingDB"
    sqlcmd -S $Servidor -E -f 65001 -W -s " | " -Q "SET NOCOUNT ON; SELECT Fuente, TipoFuente, TablaStaging, RegistrosExtraidos AS Extraidos, RegistrosGuardados AS Guardados, DuracionMs, Estado FROM OpinionesStagingDB.stg.vw_UltimoLote ORDER BY TipoFuente, Fuente; SELECT (SELECT COUNT(*) FROM SitioWebDB.dbo.Resenas) AS ResenasOrigen, (SELECT COUNT(*) FROM OpinionesStagingDB.stg.ResenasWeb) AS ResenasStaging, (SELECT COUNT(*) FROM OpinionesStagingDB.stg.ComentariosSociales) AS ComentariosStaging, (SELECT COUNT(*) FROM OpinionesStagingDB.stg.Encuestas) AS EncuestasStaging;"
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue }
    Get-Process OpinionesClientes.SocialApi -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Stop-Transcript | Out-Null
    Write-Host ""
    Write-Host "Salida guardada en $transcripcion" -ForegroundColor Green
}
