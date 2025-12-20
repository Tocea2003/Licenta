# Script pentru curățarea cache-ului și repornirea dev server-ului

Write-Host "🧹 Curățare cache..." -ForegroundColor Yellow

# Șterge node_modules/.vite
if (Test-Path "node_modules\.vite") {
    Remove-Item -Recurse -Force "node_modules\.vite"
    Write-Host "✅ Șters node_modules/.vite" -ForegroundColor Green
}

# Șterge dist
if (Test-Path "dist") {
    Remove-Item -Recurse -Force "dist"
    Write-Host "✅ Șters dist" -ForegroundColor Green
}

Write-Host ""
Write-Host "✨ Cache curățat! Rulează 'npm run dev' pentru a reporni server-ul." -ForegroundColor Cyan
Write-Host ""
Write-Host "📝 IMPORTANTE:" -ForegroundColor Yellow
Write-Host "1. Service Worker-ul este DEZACTIVAT în development" -ForegroundColor White
Write-Host "2. Deschide DevTools > Application > Service Workers și apasă 'Unregister'" -ForegroundColor White
Write-Host "3. Apoi apasă Ctrl+Shift+R pentru hard refresh" -ForegroundColor White
