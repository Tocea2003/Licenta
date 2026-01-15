# Script pentru a schimba rolul utilizatorului MogaSlaher din Admin în User
# Rulează acest script după ce backend-ul pornește

$apiUrl = "http://localhost:5022/api"

# 1. Login ca admin pentru a obține token
Write-Host "🔐 Logging in as admin..." -ForegroundColor Cyan
$loginResponse = Invoke-RestMethod -Uri "$apiUrl/Auth/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"username":"MogaSlaher","password":"parola_ta_aici"}'

$token = $loginResponse.token
Write-Host "✅ Login successful! Token obtained." -ForegroundColor Green

# 2. Obține lista de utilizatori
Write-Host "`n📋 Getting users list..." -ForegroundColor Cyan
$headers = @{
    "Authorization" = "Bearer $token"
}

$users = Invoke-RestMethod -Uri "$apiUrl/admin/users" `
    -Method GET `
    -Headers $headers

Write-Host "`nUsers:" -ForegroundColor Yellow
$users | Format-Table Id, Username, Role, CreatedAt

# 3. Găsește ID-ul utilizatorului MogaSlaher
$mogaUser = $users | Where-Object { $_.username -eq "MogaSlaher" }
if ($mogaUser) {
    Write-Host "`n🔧 Updating role for user '$($mogaUser.username)' (ID: $($mogaUser.id))..." -ForegroundColor Cyan
    
    $updateResponse = Invoke-RestMethod -Uri "$apiUrl/admin/users/$($mogaUser.id)/role" `
        -Method PATCH `
        -Headers $headers `
        -ContentType "application/json" `
        -Body '{"role":"User"}'
    
    Write-Host "✅ $($updateResponse.message)" -ForegroundColor Green
    Write-Host "`nUpdated user:" -ForegroundColor Yellow
    $updateResponse.user | Format-List
} else {
    Write-Host "❌ User MogaSlaher not found!" -ForegroundColor Red
}
