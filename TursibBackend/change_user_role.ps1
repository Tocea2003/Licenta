# Script simplu pentru a schimba rolul utilizatorului MogaSlaher din Admin în User
# Folosește direct conexiunea la baza de date SQLite

param(
    [string]$Username = "MogaSlaher",
    [string]$NewRole = "User"
)

$dbPath = "D:\Licenta\TursibBackend\TursibDb.db"

if (-not (Test-Path $dbPath)) {
    Write-Host "❌ Database not found at $dbPath" -ForegroundColor Red
    exit 1
}

Write-Host "🔧 Updating user role in database..." -ForegroundColor Cyan
Write-Host "Database: $dbPath" -ForegroundColor Gray
Write-Host "Username: $Username" -ForegroundColor Gray
Write-Host "New Role: $NewRole" -ForegroundColor Gray

# Încarcă assembly-ul pentru SQLite
Add-Type -Path "C:\Users\$env:USERNAME\.nuget\packages\microsoft.data.sqlite.core\9.0.0\lib\net8.0\Microsoft.Data.Sqlite.dll" -ErrorAction SilentlyContinue

try {
    # Conexiune la baza de date
    $connectionString = "Data Source=$dbPath"
    $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
    $connection.Open()
    
    # Verifică dacă utilizatorul există
    $selectCmd = $connection.CreateCommand()
    $selectCmd.CommandText = "SELECT Id, Username, Role FROM Users WHERE Username = @username"
    $selectCmd.Parameters.AddWithValue("@username", $Username) | Out-Null
    
    $reader = $selectCmd.ExecuteReader()
    
    if ($reader.Read()) {
        $userId = $reader["Id"]
        $currentRole = $reader["Role"]
        $reader.Close()
        
        Write-Host "`n✅ User found:" -ForegroundColor Green
        Write-Host "   ID: $userId"
        Write-Host "   Username: $Username"
        Write-Host "   Current Role: $currentRole"
        
        # Update rolul
        $updateCmd = $connection.CreateCommand()
        $updateCmd.CommandText = "UPDATE Users SET Role = @role WHERE Id = @id"
        $updateCmd.Parameters.AddWithValue("@role", $NewRole) | Out-Null
        $updateCmd.Parameters.AddWithValue("@id", $userId) | Out-Null
        
        $rowsAffected = $updateCmd.ExecuteNonQuery()
        
        if ($rowsAffected -gt 0) {
            Write-Host "`n✅ Role updated successfully!" -ForegroundColor Green
            Write-Host "   $Username is now: $NewRole" -ForegroundColor Cyan
        } else {
            Write-Host "`n⚠️  No changes made" -ForegroundColor Yellow
        }
    } else {
        $reader.Close()
        Write-Host "`n❌ User '$Username' not found in database" -ForegroundColor Red
    }
    
    $connection.Close()
    
} catch {
    Write-Host "`n❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nFalling back to direct SQL execution..." -ForegroundColor Yellow
    
    # Fallback: generează SQL script pe care îl poate rula manual
    $sqlScript = @"
-- Verifică utilizatorul
SELECT Id, Username, Role FROM Users WHERE Username = '$Username';

-- Update rolul
UPDATE Users SET Role = '$NewRole' WHERE Username = '$Username';

-- Verifică modificarea
SELECT Id, Username, Role FROM Users WHERE Username = '$Username';
"@
    
    $sqlFile = "D:\Licenta\TursibBackend\temp_update_role.sql"
    $sqlScript | Out-File -FilePath $sqlFile -Encoding UTF8
    
    Write-Host "`n📝 SQL script saved to: $sqlFile" -ForegroundColor Cyan
    Write-Host "`nYou can execute it manually using:" -ForegroundColor Yellow
    Write-Host "  1. Install DB Browser for SQLite (https://sqlitebrowser.org/)" -ForegroundColor Gray
    Write-Host "  2. Open TursibDb.db" -ForegroundColor Gray
    Write-Host "  3. Go to 'Execute SQL' tab" -ForegroundColor Gray
    Write-Host "  4. Run the queries from temp_update_role.sql" -ForegroundColor Gray
}
