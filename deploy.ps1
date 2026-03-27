# Deploy the 7nes mod to 7 Days To Die Mods folder
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppId = "251570"  # 7 Days to Die

# --- Find 7DTD via Steam registry + libraryfolders.vdf ---
function Find-GameDir {
    $steamPath = $null
    foreach ($key in @(
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam",
        "HKLM:\SOFTWARE\Valve\Steam",
        "HKCU:\SOFTWARE\Valve\Steam"
    )) {
        $reg = Get-ItemProperty -Path $key -Name InstallPath -ErrorAction SilentlyContinue
        if ($reg) { $steamPath = $reg.InstallPath; break }
    }
    if (-not $steamPath) {
        Write-Host "Steam not found in registry." -ForegroundColor Red
        exit 1
    }

    $vdf = Join-Path $steamPath "steamapps\libraryfolders.vdf"
    if (-not (Test-Path $vdf)) {
        Write-Host "libraryfolders.vdf not found at $vdf" -ForegroundColor Red
        exit 1
    }

    $content = Get-Content $vdf -Raw
    $libraries = [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' }

    foreach ($lib in $libraries) {
        $manifest = Join-Path $lib "steamapps\appmanifest_$AppId.acf"
        if (Test-Path $manifest) {
            $acf = Get-Content $manifest -Raw
            $m = [regex]::Match($acf, '"installdir"\s+"([^"]+)"')
            if ($m.Success) {
                $dir = Join-Path $lib "steamapps\common\$($m.Groups[1].Value)"
                if (Test-Path $dir) { return $dir }
            }
        }
    }

    Write-Host "7 Days to Die (AppId $AppId) not found in any Steam library." -ForegroundColor Red
    exit 1
}

$GamePath = Find-GameDir
Write-Host "Found 7DTD at: $GamePath" -ForegroundColor Yellow
$ModDest = Join-Path $GamePath "Mods\7nes"

Write-Host "=== 7nes Deploy ==="

# Always rebuild to ensure latest source is deployed
Write-Host "Building..."
Push-Location "$ScriptDir\src"
& dotnet build --nologo -v q /p:SevenDaysToDiePath="$GamePath"
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Build failed" }
Pop-Location

# Create mod destination
New-Item -ItemType Directory -Path "$ModDest\Config" -Force | Out-Null
New-Item -ItemType Directory -Path "$ModDest\Roms" -Force | Out-Null

# Copy mod files
Write-Host "Deploying to: $ModDest"

Copy-Item "$ScriptDir\ModInfo.xml" "$ModDest\" -Force
Copy-Item "$ScriptDir\Config\*" "$ModDest\Config\" -Recurse -Force

# Copy DLL (may fail if game is running and has the file locked)
try {
    Copy-Item "$ScriptDir\7nes.dll" "$ModDest\" -Force
} catch {
    Write-Host "WARNING: Could not copy 7nes.dll (game may be running). Restart the game and re-deploy." -ForegroundColor Yellow
}

# Copy Resources (asset bundles) if present
$ResourcesDir = Join-Path $ScriptDir "Resources"
if (Test-Path $ResourcesDir) {
    New-Item -ItemType Directory -Path "$ModDest\Resources" -Force | Out-Null
    Copy-Item "$ResourcesDir\*" "$ModDest\Resources\" -Force -ErrorAction SilentlyContinue
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
