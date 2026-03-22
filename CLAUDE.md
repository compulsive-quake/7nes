# 7nes - NES Emulator TV for 7 Days to Die

## Project Overview

7nes is a 7 Days to Die mod that adds an NES emulator TV block to the game. Players can craft and place the NES TV block, then interact with it to launch a fully functional NES emulator overlay. Load your own .nes ROM files and play classic NES games inside 7 Days to Die.

## Architecture

- **C# Harmony mod** that patches into 7 Days to Die at runtime
- **Embedded NES emulator** that renders frames to a Unity `Texture2D`
- The emulator output is displayed via an XUi window overlay (`nesEmulator`) when the player activates the TV block
- Input is captured from Unity's `Input` system and mapped to NES controller buttons

## Mod Structure

```
7nes/
├── CLAUDE.md                 # This file
├── ModInfo.xml               # Mod metadata for 7DTD mod loader
├── .gitignore                # Git ignore rules
├── Config/
│   ├── blocks.xml            # XPath patch adding the nesTV block
│   ├── localization.txt      # Display names and descriptions
│   └── windows.xml           # XUi window definition for emulator overlay
├── src/
│   ├── 7nes.csproj           # C# project file
│   ├── Core/
│   │   ├── Cpu.cs            # 6502 CPU - all 151 official opcodes
│   │   ├── Ppu.cs            # Picture Processing Unit - scanline renderer
│   │   ├── Nes.cs            # Main emulator - memory map, frame loop
│   │   ├── Cartridge.cs      # iNES ROM parser
│   │   ├── Controller.cs     # NES controller (strobe/shift register)
│   │   ├── Mapper.cs         # IMapper interface
│   │   ├── Mapper0.cs        # NROM (no bank switching)
│   │   ├── Mapper1.cs        # MMC1 (shift register banking)
│   │   ├── Mapper2.cs        # UxROM (PRG bank switching)
│   │   └── Mapper3.cs        # CNROM (CHR bank switching)
│   └── Integration/
│       ├── ModInit.cs        # IModApi entry point, Harmony patching
│       ├── NesEmulatorManager.cs  # Singleton: emulator + Texture2D rendering
│       ├── NesEmulatorWindow.cs   # IMGUI overlay: screen + ROM selector
│       └── BlockInteractionPatch.cs # Harmony patches for nesTV block
├── Roms/                     # Place .nes ROM files here (not tracked in git)
└── 7nes.dll                  # Compiled mod assembly (build output)
```

## Supported Mappers

- **Mapper 0 (NROM)** - Super Mario Bros, Donkey Kong, etc.
- **Mapper 1 (MMC1)** - Legend of Zelda, Metroid, etc.
- **Mapper 2 (UxROM)** - Mega Man, Castlevania, etc.
- **Mapper 3 (CNROM)** - Gradius, etc.

## In-Game Usage

1. Build the mod (`dotnet build` in src/)
2. Copy 7nes folder to `7 Days to Die/Mods/`
3. Place `.nes` ROM files in `Mods/7nes/Roms/`
4. In-game: find "NES TV" in creative menu or craft it
5. Place the TV block and press **E** to open the emulator
6. Select a ROM from the list, then play
7. Press **Tab** to switch ROMs, **F5** to reset, **Escape** to close

## Build Instructions

1. Ensure 7 Days to Die is installed. The project expects it at:
   `C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die`
   Edit `SevenDaysToDiePath` in `src/7nes.csproj` if your install path differs.

2. Compile the mod:
   ```
   cd src
   dotnet build
   ```
   The build copies the output DLL to the project root automatically.

3. Copy or symlink the entire `7nes` folder into your 7 Days to Die `Mods/` directory.

## ROM Loading

Place `.nes` ROM files in the `Mods/7nes/Roms/` folder. The emulator will detect and list available ROMs when you activate the NES TV block.

## Key Controls

| Key        | NES Button |
|------------|------------|
| Arrow Keys | D-pad      |
| A          | A          |
| D          | B          |
| Enter      | Start      |
| Right Shift| Select     |
| Escape     | Close emulator |

## Workflow Rules

- After completing any task that changes mod files (C# source, XML configs, etc.), always build and deploy by running:
  ```
  powershell -ExecutionPolicy Bypass -File deploy.ps1
  ```
  This rebuilds the DLL and copies all mod files to the game's Mods folder.

## 7DTD Modding Knowledgebase

A shared knowledgebase for 7 Days to Die modding is located at `../7KB/`. It contains detailed documentation on:

- Mod structure, XML patching (XPath), localization, blocks, entities, paint/textures
- XUi window system (XML) and XUi controllers (C#)
- Prefab binary formats (TTS, NIM), block data structures, block definition resolution
- Coordinate system, rotation system (24 orientations), multi-block rotation, X-mirror rotation

Consult the [knowledgebase README](../7KB/README.md) for the full index. Reference these docs when working on any 7DTD modding task.

## Dependencies

- 7 Days to Die (provides Assembly-CSharp, UnityEngine, and Harmony assemblies)
- .NET 8.0 SDK (for building; targets net48 for Unity/Mono compatibility)
