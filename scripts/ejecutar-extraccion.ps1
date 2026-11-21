<#
  Inicia la API simulada de redes sociales en otra ventana y luego ejecuta el Worker de extracción.
  Requisitos: haber ejecutado database\00 y database\01 en SQL Server y scripts\configurar-secretos.ps1
#>
$raiz = Split-Path -Parent $PSScriptRoot

Write-Host "Iniciando la API de redes sociales en http://localhost:5080 ..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project `"$raiz\src\OpinionesClientes.SocialApi`""

# Espera a que la API responda
for ($i = 0; $i -lt 30; $i++) {
    try { Invoke-RestMethod http://localhost:5080/health -TimeoutSec 2 | Out-Null; break } catch { Start-Sleep -Seconds 1 }
}

Write-Host "Ejecutando el Worker de extracción ..." -ForegroundColor Cyan
dotnet run --project "$raiz\src\OpinionesClientes.Worker"
