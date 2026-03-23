# Deploy the 7nes mod to 7 Days To Die Mods folder
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$GamePath = "D:\SteamLibrary\steamapps\common\7 Days To Die"
$ModDest = Join-Path $GamePath "Mods\7nes"

Write-Host "=== 7nes Deploy ==="

# Always rebuild to ensure latest source is deployed
Write-Host "Building..."
Push-Location "$ScriptDir\src"
& dotnet build --nologo -v q
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Build failed" }
Pop-Location

# Create mod destination
New-Item -ItemType Directory -Path "$ModDest\Config" -Force | Out-Null
New-Item -ItemType Directory -Path "$ModDest\Roms" -Force | Out-Null

# Copy mod files
Write-Host "Deploying to: $ModDest"

Copy-Item "$ScriptDir\ModInfo.xml" "$ModDest\" -Force
Copy-Item "$ScriptDir\7nes.dll" "$ModDest\" -Force
Copy-Item "$ScriptDir\Config\blocks.xml" "$ModDest\Config\" -Force
Copy-Item "$ScriptDir\Config\windows.xml" "$ModDest\Config\" -Force
Copy-Item "$ScriptDir\Config\localization.txt" "$ModDest\Config\" -Force

# Copy Resources (asset bundles) if present
$ResourcesDir = Join-Path $ScriptDir "Resources"
if (Test-Path $ResourcesDir) {
    New-Item -ItemType Directory -Path "$ModDest\Resources" -Force | Out-Null
    Copy-Item "$ResourcesDir\nesmodel.unity3d" "$ModDest\Resources\" -Force -ErrorAction SilentlyContinue
    Write-Host "Copied asset bundles."
}

# Copy UIAtlases if present
$UIAtlasDir = Join-Path $ScriptDir "UIAtlases"
if (Test-Path $UIAtlasDir) {
    Copy-Item $UIAtlasDir "$ModDest\" -Recurse -Force
}

# Copy ROMs if any exist in source
$RomsDir = Join-Path $ScriptDir "Roms"
if (Test-Path $RomsDir) {
    if (Get-ChildItem "$RomsDir\*.nes" -ErrorAction SilentlyContinue) {
        Write-Host "Copying ROMs..."
        Copy-Item "$RomsDir\*.nes" "$ModDest\Roms\" -Force
    }
    # Copy box art folder if present
    $BoxDir = Join-Path $RomsDir "box"
    if (Test-Path $BoxDir) {
        New-Item -ItemType Directory -Path "$ModDest\Roms\box" -Force | Out-Null
        Copy-Item "$BoxDir\*" "$ModDest\Roms\box\" -Force
        Write-Host "Copied box art."
    }
    # Copy cart art folder if present
    $CartDir = Join-Path $RomsDir "Cart"
    if (Test-Path $CartDir) {
        New-Item -ItemType Directory -Path "$ModDest\Roms\Cart" -Force | Out-Null
        Copy-Item "$CartDir\*" "$ModDest\Roms\Cart\" -Force
        Write-Host "Copied cart art."
    }
}

Write-Host ""
Write-Host "Deployed successfully!" -ForegroundColor Green
Write-Host "  Mod location: $ModDest"
Write-Host "  ROM folder:   $ModDest\Roms\"
Write-Host ""
Write-Host "Place .nes ROM files in the Roms folder, then launch the game."
