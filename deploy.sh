#!/bin/bash
# Deploy the 7nes mod to 7 Days To Die Mods folder
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
GAME_PATH="D:/SteamLibrary/steamapps/common/7 Days To Die"
MOD_DEST="$GAME_PATH/Mods/7nes"

echo "=== 7nes Deploy ==="

# Build first if DLL doesn't exist
if [ ! -f "$SCRIPT_DIR/7nes.dll" ]; then
    echo "DLL not found, building first..."
    bash "$SCRIPT_DIR/build.sh"
fi

# Create mod destination
mkdir -p "$MOD_DEST/Config"
mkdir -p "$MOD_DEST/Roms"

# Copy mod files
echo "Deploying to: $MOD_DEST"

cp "$SCRIPT_DIR/ModInfo.xml"            "$MOD_DEST/"
cp "$SCRIPT_DIR/7nes.dll"               "$MOD_DEST/"
cp "$SCRIPT_DIR/Config/blocks.xml"      "$MOD_DEST/Config/"
cp "$SCRIPT_DIR/Config/windows.xml"     "$MOD_DEST/Config/"
cp "$SCRIPT_DIR/Config/localization.txt" "$MOD_DEST/Config/"

# Copy ROMs if any exist in source
if [ -d "$SCRIPT_DIR/Roms" ] && [ "$(ls -A "$SCRIPT_DIR/Roms/" 2>/dev/null)" ]; then
    echo "Copying ROMs..."
    cp "$SCRIPT_DIR/Roms/"*.nes "$MOD_DEST/Roms/" 2>/dev/null || true
fi

echo ""
echo "Deployed successfully!"
echo "  Mod location: $MOD_DEST"
echo "  ROM folder:   $MOD_DEST/Roms/"
echo ""
echo "Place .nes ROM files in the Roms folder, then launch the game."
