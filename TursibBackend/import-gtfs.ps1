#!/usr/bin/env pwsh
# Script pentru import GTFS în baza de date

Write-Host "🚌 Tursib GTFS Importer" -ForegroundColor Blue
Write-Host "========================`n" -ForegroundColor Blue

# Verifică dacă există fișierul GTFS
$gtfsPath = "D:\Licenta\tursib.gtfs_2025-10-v1"
if (-not (Test-Path $gtfsPath)) {
    Write-Host "❌ GTFS directory not found: $gtfsPath" -ForegroundColor Red
    exit 1
}

Write-Host "📁 GTFS path: $gtfsPath" -ForegroundColor Green
Write-Host "🔧 Starting import...`n" -ForegroundColor Yellow

# Rulează programul
$env:DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION = $true
dotnet run --project TursibBackend.csproj --no-build -- import-gtfs

Write-Host "`n✅ Import process finished!" -ForegroundColor Green
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
