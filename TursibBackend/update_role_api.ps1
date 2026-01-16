# Script pentru a schimba rolul utilizatorului prin API
param(
    [string]$ApiUrl = "http://localhost:5022/api",
    [string]$AdminUser = "MogaSlaher",
    [SecureString]$AdminPassword,
    [string]$TargetUsername = "MogaSlaher",
    [string]$NewRole = "User"
)

if ($null -eq $AdminPassword) {
    $AdminPassword = Read-Host "Enter password for $AdminUser" -AsSecureString
}

# Convert SecureString to plain text for API call
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdminPassword)
$PlainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
[System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)

Write-Host "Step 1: Logging in as admin..." -ForegroundColor Cyan

$loginBody = @{
    username = $AdminUser
    password = $PlainPassword
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$ApiUrl/Auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $loginBody
    
    Write-Host "Login successful!" -ForegroundColor Green
    $token = $loginResponse.token
    
    Write-Host "`nStep 2: Getting users list..." -ForegroundColor Cyan
    $headers = @{
        "Authorization" = "Bearer $token"
    }
    
    $users = Invoke-RestMethod -Uri "$ApiUrl/admin/users" `
        -Method GET `
        -Headers $headers
    
    Write-Host "`nUsers in database:" -ForegroundColor Yellow
    $users | Format-Table Id, Username, Role, CreatedAt
    
    $targetUser = $users | Where-Object { $_.username -eq $TargetUsername }
    
    if ($targetUser) {
        Write-Host "Step 3: Updating role for $TargetUsername (ID: $($targetUser.id))..." -ForegroundColor Cyan
        
        $roleBody = @{
            role = $NewRole
        } | ConvertTo-Json
        
        $updateResponse = Invoke-RestMethod -Uri "$ApiUrl/admin/users/$($targetUser.id)/role" `
            -Method PATCH `
            -Headers $headers `
            -ContentType "application/json" `
            -Body $roleBody
        
        Write-Host "`nSUCCESS!" -ForegroundColor Green
        Write-Host $updateResponse.message -ForegroundColor Cyan
        Write-Host "`nUpdated user details:" -ForegroundColor Yellow
        $updateResponse.user | Format-List
    } else {
        Write-Host "ERROR: User '$TargetUsername' not found!" -ForegroundColor Red
    }
    
} catch {
    Write-Host "`nERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
} finally {
    # Clear sensitive data from memory
    if ($PlainPassword) {
        $PlainPassword = $null
    }
}
