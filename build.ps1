# Build the 7nes mod DLL
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SrcDir = Join-Path $ScriptDir "src"
$GamePath = "D:\SteamLibrary\steamapps\common\7 Days To Die"

Write-Host "=== 7nes Build ==="

# Verify game assemblies exist
if (-not (Test-Path "$GamePath\7DaysToDie_Data\Managed")) {
    Write-Host "ERROR: 7 Days To Die not found at: $GamePath" -ForegroundColor Red
    Write-Host "Update GamePath in this script and SevenDaysToDiePath in src\7nes.csproj"
    exit 1
}

# Build
Write-Host "Building..."
Push-Location $SrcDir
try {
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
} finally {
    Pop-Location
}

# Copy DLL to mod root
Copy-Item "$ScriptDir\bin\Release\net48\7nes.dll" "$ScriptDir\7nes.dll" -Force

Write-Host ""
Write-Host "Build complete: $ScriptDir\7nes.dll" -ForegroundColor Green
Write-Host "Run .\deploy.ps1 to install to game"
