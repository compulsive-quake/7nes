# Build, package, and publish a GitHub release for the 7nes mod
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# --- Read version from ModInfo.xml ---
$ModInfoPath = Join-Path $ScriptDir "ModInfo.xml"
[xml]$modInfo = Get-Content $ModInfoPath
$Version = $modInfo.xml.Version.value
if (-not $Version) { throw "Could not read version from ModInfo.xml" }
$Tag = "v$Version"

Write-Host "=== 7nes Release $Tag ===" -ForegroundColor Cyan

# --- Check for uncommitted changes ---
$status = git -C $ScriptDir status --porcelain
if ($status) {
    Write-Host "ERROR: You have uncommitted changes. Commit or stash them first." -ForegroundColor Red
    git -C $ScriptDir status --short
    exit 1
}

# --- Check tag doesn't already exist ---
$existingTag = git -C $ScriptDir tag -l $Tag
if ($existingTag) {
    Write-Host "ERROR: Tag $Tag already exists. Bump the version in ModInfo.xml first." -ForegroundColor Red
    exit 1
}

# --- Extract changelog for this version ---
$changelog = Get-Content (Join-Path $ScriptDir "CHANGELOG.md") -Raw
$pattern = "(?ms)^## \[$([regex]::Escape($Version))\].*?$(.+?)(?=^## \[|\z)"
$match = [regex]::Match($changelog, $pattern)
if (-not $match.Success) {
    Write-Host "WARNING: No changelog entry found for version $Version" -ForegroundColor Yellow
    $releaseNotes = "Release $Tag"
} else {
    $releaseNotes = $match.Groups[0].Value.Trim()
}

Write-Host ""
Write-Host "Release notes:" -ForegroundColor Yellow
Write-Host $releaseNotes
Write-Host ""

# --- Build ---
Write-Host "Building..." -ForegroundColor Yellow
Push-Location (Join-Path $ScriptDir "src")
& dotnet build --nologo -v q
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Build failed" }
Pop-Location

# --- Package into zip ---
$ZipName = "7nes-$Tag.zip"
$ZipPath = Join-Path $ScriptDir $ZipName
$StagingDir = Join-Path $env:TEMP "7nes-release-staging"

if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
New-Item -ItemType Directory -Path "$StagingDir\7nes" -Force | Out-Null

# Copy mod files into staging
Copy-Item "$ScriptDir\ModInfo.xml"   "$StagingDir\7nes\" -Force
Copy-Item "$ScriptDir\7nes.dll"      "$StagingDir\7nes\" -Force
Copy-Item "$ScriptDir\CHANGELOG.md"  "$StagingDir\7nes\" -Force
Copy-Item "$ScriptDir\Config"        "$StagingDir\7nes\Config" -Recurse -Force
Copy-Item "$ScriptDir\Resources"     "$StagingDir\7nes\Resources" -Recurse -Force
Copy-Item "$ScriptDir\UIAtlases"     "$StagingDir\7nes\UIAtlases" -Recurse -Force

# Create empty Roms folder with placeholder
New-Item -ItemType Directory -Path "$StagingDir\7nes\Roms\box" -Force | Out-Null
New-Item -ItemType Directory -Path "$StagingDir\7nes\Roms\Cart" -Force | Out-Null
Set-Content "$StagingDir\7nes\Roms\PUT_NES_ROMS_HERE.txt" "Place .nes ROM files in this folder.`nBox art PNGs go in the box/ subfolder.`nCart label PNGs go in the Cart/ subfolder."

# Create zip
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Write-Host "Packaging $ZipName..." -ForegroundColor Yellow
Compress-Archive -Path "$StagingDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal

# Clean up staging
Remove-Item $StagingDir -Recurse -Force

$zipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
Write-Host "Created $ZipName ($zipSize MB)" -ForegroundColor Green

# --- Create git tag and GitHub release ---
Write-Host "Creating tag $Tag..." -ForegroundColor Yellow
git -C $ScriptDir tag -a $Tag -m "Release $Tag"
git -C $ScriptDir push origin $Tag

Write-Host "Creating GitHub release..." -ForegroundColor Yellow
$notesFile = Join-Path $env:TEMP "7nes-release-notes.md"
Set-Content $notesFile $releaseNotes -Encoding UTF8

gh release create $Tag $ZipPath --title "7nes $Tag" --notes-file $notesFile --repo "compulsive-quake/7nes"

Remove-Item $notesFile -Force -ErrorAction SilentlyContinue
Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Released $Tag successfully!" -ForegroundColor Green
