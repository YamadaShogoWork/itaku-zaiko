# DB Reset Script
Set-Location $PSScriptRoot

Write-Host "[1/3] Stopping dotnet..."
$procs = Get-Process -Name dotnet -ErrorAction SilentlyContinue
if ($procs) {
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 2
    Write-Host "  Done."
} else {
    Write-Host "  No dotnet process found."
}

Write-Host "[2/3] Deleting app.db..."
$dbPath = Join-Path $PSScriptRoot "Zaiko\app.db"
if (Test-Path $dbPath) {
    try {
        Remove-Item $dbPath -Force -ErrorAction Stop
    } catch {
        # fallback: cmd del (handles some lock situations Remove-Item can't)
        cmd /c "del /F /Q `"$dbPath`"" 2>$null
    }

    if (Test-Path $dbPath) {
        Write-Host "  Failed to delete app.db. Close all apps using it and retry." -ForegroundColor Red
        pause
        exit 1
    }
    Write-Host "  app.db deleted."
} else {
    Write-Host "  app.db not found (skipped)."
}

Write-Host "[3/3] Running migrations..."
dotnet ef database update --project Zaiko
if ($LASTEXITCODE -ne 0) {
    Write-Host "Migration error." -ForegroundColor Red
    pause
    exit 1
}

Write-Host ""
Write-Host "DB reset complete." -ForegroundColor Green
pause
