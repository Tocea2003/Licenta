#!/usr/bin/env pwsh
# Import GTFS în baza de date configurată (ConnectionStrings:DefaultConnection)
# Utilizare: ./import-gtfs.ps1 -GtfsPath "D:\Licenta\tursib.gtfs_2025-10-v1"
param([Parameter(Mandatory = $true)][string]$GtfsPath)

if (-not (Test-Path $GtfsPath)) {
    Write-Host "❌ GTFS directory not found: $GtfsPath" -ForegroundColor Red
    exit 1
}

dotnet run --project TursibBackend.csproj -- import-gtfs $GtfsPath
