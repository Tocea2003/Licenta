# Stop any running TursibBackend processes
Write-Host "🛑 Stopping TursibBackend..." -ForegroundColor Yellow
Get-Process | Where-Object {$_.ProcessName -like "*TursibBackend*"} | Stop-Process -Force -ErrorAction SilentlyContinue

# Wait a moment for processes to fully stop
Start-Sleep -Seconds 1

# Rebuild the project
Write-Host "🔨 Rebuilding TursibBackend..." -ForegroundColor Cyan
dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build successful! Starting server..." -ForegroundColor Green
    # Run the backend
    dotnet run
} else {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}
