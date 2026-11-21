<#
  Guarda la API key de la API de redes sociales en User Secrets (fuera del repositorio).
  El Worker y la API comparten el mismo UserSecretsId, así que basta con ejecutarlo una vez.

  Uso:  .\scripts\configurar-secretos.ps1            (genera una clave aleatoria)
        .\scripts\configurar-secretos.ps1 -ApiKey "mi-clave"
#>
param(
    [string]$ApiKey = [guid]::NewGuid().ToString("N")
)

$raiz = Split-Path -Parent $PSScriptRoot
dotnet user-secrets set "SocialApi:ApiKey" $ApiKey --project "$raiz\src\OpinionesClientes.Worker" | Out-Null

Write-Host "API key guardada en User Secrets (no queda en appsettings.json ni en GitHub)." -ForegroundColor Green
Write-Host "Ubicación: %APPDATA%\Microsoft\UserSecrets\opiniones-clientes-extraccion-7f3c2a91\secrets.json"
