# Compila todo y genera el instalador único:
#   dist\Instalar_Spotify_Local_Streamlabs.exe
#
# Requisitos (Windows 10/11):
#   - Node.js 22  (winget install OpenJS.NodeJS.LTS)
#   - .NET SDK 9  (winget install Microsoft.DotNet.SDK.9)
#
# Uso:  powershell -ExecutionPolicy Bypass -File build.ps1

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Dist = Join-Path $Root "dist"
$Res  = Join-Path $Root "installer\Resources"

function Step($t) { Write-Host "`n=== $t ===" -ForegroundColor Cyan }

New-Item -ItemType Directory -Force -Path $Dist, $Res | Out-Null

Step "1/3 Servidor (Node.js -> SpotifyOverlayServer.exe)"
Push-Location (Join-Path $Root "server")
npm ci
if ($LASTEXITCODE -ne 0) { throw "npm ci falló" }
npm run build
if ($LASTEXITCODE -ne 0) { throw "pkg falló" }
Pop-Location
Copy-Item (Join-Path $Root "server\dist\SpotifyOverlayServer.exe") $Res -Force

Step "2/3 Lanzador de bandeja (SpotifyOverlay.exe)"
dotnet build (Join-Path $Root "launcher\SpotifyOverlay.csproj") -c Release
if ($LASTEXITCODE -ne 0) { throw "Compilación del lanzador falló" }
Copy-Item (Join-Path $Root "launcher\bin\Release\net48\SpotifyOverlay.exe") $Res -Force

Step "3/3 Instalador (exe único autocontenido)"
dotnet publish (Join-Path $Root "installer\SpotifyOverlayStreamlabsInstaller.csproj") -c Release -o (Join-Path $Root "installer\bin\publish")
if ($LASTEXITCODE -ne 0) { throw "Publicación del instalador falló" }
Copy-Item (Join-Path $Root "installer\bin\publish\Instalar_Spotify_Local_Streamlabs.exe") $Dist -Force

Write-Host "`nOK - Listo: $Dist\Instalar_Spotify_Local_Streamlabs.exe" -ForegroundColor Green
