#!/bin/bash
# Build the 7nes mod DLL
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_DIR="$SCRIPT_DIR/src"
GAME_PATH="D:/SteamLibrary/steamapps/common/7 Days To Die"

echo "=== 7nes Build ==="

# Verify game assemblies exist
if [ ! -d "$GAME_PATH/7DaysToDie_Data/Managed" ]; then
    echo "ERROR: 7 Days To Die not found at: $GAME_PATH"
    echo "Update GAME_PATH in this script and SevenDaysToDiePath in src/7nes.csproj"
    exit 1
fi

# Build
echo "Building..."
cd "$SRC_DIR"
dotnet build -c Release

# Copy DLL to mod root
cp "$SRC_DIR/bin/Release/net6.0/7nes.dll" "$SCRIPT_DIR/7nes.dll"

echo ""
echo "Build complete: $SCRIPT_DIR/7nes.dll"
echo "Run ./deploy.sh to install to game"
