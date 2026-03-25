# 7nes - NES Emulator TV for 7 Days to Die

## Project Overview

7nes is a 7 Days to Die mod that adds a playable NES emulator to the game. Players place an NES TV block (the screen) and an NES Console block (the cartridge slot), connect power, and insert game cartridge items to play classic NES games inside 7 Days to Die.

## Architecture

- **C# Harmony mod** that patches into 7 Days to Die at runtime
- **Embedded NES emulator** that renders frames to a Unity `Texture2D`
- The emulator output is displayed on an in-world screen quad attached to the nesTV block, with an IMGUI overlay for ROM selection and controls
- Input is captured from Unity's `Input` system and mapped to NES controller buttons

## In-Game Objects

### nesTV (Block)
- **Class:** `Powered` (extends `tvSmallStand1x1`)
- **Purpose:** The screen that displays emulator output. Requires 5 power via wire tool.
- **Interaction:** Hold E for radial menu with Play, Choose Game, Turn On/Off, Controls options. Harmony patches on `BlockPowered` handle all interaction (see `BlockInteractionPatch.cs`).
- **Display:** Emulator frames render to a Unity quad positioned on the TV's screen face, calibrated per rotation.

### nesConsole (Block)
- **Class:** `Workstation`
- **Purpose:** The NES console unit where players insert game cartridges.
- **Interaction:** Press E to open a workstation UI with a single tool slot ("CARTRIDGE SLOT") for inserting cartridge items.
- **Config:** Uses `Modules="tools,output"`, `ToolNames="1"`, `CraftingAreaRecipes="nesConsole"` (no actual recipes — the crafting list is intentionally empty).
- **XUi:** Window group `workstation_nesConsole` defined in `Config/XUi/xui.xml`, with custom tool slot window `windowNesConsoleSlot` in `Config/XUi/windows.xml`.

### nesCart_* (Items — dynamically generated)
- **Generation:** `NesCartridgeItems.cs` scans `Roms/` at mod init and generates `Config/items.xml` with one item per `.nes` ROM file.
- **Naming:** ROM filename is sanitized into item name (e.g., `Contra (USA).nes` → `nesCart_ContraUSA`).
- **Model:** Uses the NES cartridge prefab from `Resources/nescartridge.unity3d`.
- **Icons:** Box art PNGs from `Roms/box/` are copied to `UIAtlases/ItemIconAtlas/` as custom icons.
- **Hand adjustments:** `CartridgeHandAdjuster.cs` fixes scale/material when held, `CartridgeMaterialFixer.cs` fixes cart label textures.

## Mod Structure

```
7nes/
├── CLAUDE.md                 # This file
├── ModInfo.xml               # Mod metadata for 7DTD mod loader
├── .gitignore                # Git ignore rules
├── Config/
│   ├── blocks.xml            # XPath patch: nesTV and nesConsole blocks
│   ├── items.xml             # Auto-generated: cartridge items (one per ROM)
│   ├── loot.xml              # Loot container for nesConsole storage
│   ├── localization.txt      # Display names and descriptions
│   ├── windows.xml           # XUi window definition for emulator overlay
│   └── XUi/
│       ├── windows.xml       # Workstation tool slot window (nesConsole)
│       └── xui.xml           # Window group for nesConsole workstation
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
│       ├── ModInit.cs             # IModApi entry point, Harmony patching
│       ├── NesEmulatorManager.cs  # Singleton: emulator + Texture2D rendering
│       ├── NesEmulatorWindow.cs   # IMGUI overlay: screen + ROM selector + controls rebind
│       ├── NesInputBindings.cs    # Configurable keyboard/gamepad bindings
│       ├── NesAudioPlayer.cs      # Unity AudioSource for APU output
│       ├── NesCartridgeItems.cs   # Dynamic item/icon generation from ROMs
│       ├── CartridgeHandAdjuster.cs   # Fix held cartridge scale/material
│       ├── CartridgeMaterialFixer.cs  # Fix cart label textures at runtime
│       └── BlockInteractionPatch.cs   # Harmony patches for nesTV block interaction
├── UnityProject/              # Unity project for building asset bundles
│   └── Assets/
│       └── NESModel/          # NES console 3D model and prefab
├── Resources/
│   ├── nescartridge.unity3d   # Cartridge model asset bundle
│   └── nesmodel.unity3d       # NES console model asset bundle
├── UIAtlases/
│   ├── UIAtlas/               # Custom UI sprites (radial menu icons)
│   │   └── nes_cartridge.png
│   └── ItemIconAtlas/         # Auto-generated cartridge item icons
├── Roms/                      # Place .nes ROM files here (not tracked in git)
│   ├── box/                   # Box art PNGs (filename matches ROM name)
│   └── Cart/                  # Cart label art PNGs
└── 7nes.dll                   # Compiled mod assembly (build output)
```

## Supported Mappers

- **Mapper 0 (NROM)** - Super Mario Bros, Donkey Kong, etc.
- **Mapper 1 (MMC1)** - Legend of Zelda, Metroid, etc.
- **Mapper 2 (UxROM)** - Mega Man, Castlevania, etc.
- **Mapper 3 (CNROM)** - Gradius, etc.

## In-Game Usage

1. Build the mod (`dotnet build` in src/)
2. Copy 7nes folder to `7 Days to Die/Mods/`
3. Place `.nes` ROM files in `Mods/7nes/Roms/` (each ROM auto-generates a cartridge item)
4. In-game: find "NES TV", "NES Console", and cartridge items in the creative menu
5. Place the **NES TV** block and connect it to power (requires 5W via wire tool)
6. Place the **NES Console** block nearby, press **E** to open the cartridge slot, and insert a cartridge item
7. Interact with the **NES TV**: press **E** to play, or hold **E** for the radial menu (Choose Game, Turn On/Off, Controls)
8. Press **Tab** to switch ROMs, **F5** to reset, **E** to exit play mode

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
| E          | Close emulator (during play) |
| Escape     | Close ROM list |

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

Consult the [knowledgebase README](../7KB/README.md) for the full index. Reference these docs when working on any 7DTD modding task. When you learn new things about modding the game, update the knowledgebase with that information.

## Dependencies

- 7 Days to Die (provides Assembly-CSharp, UnityEngine, and Harmony assemblies)
- .NET 8.0 SDK (for building; targets net48 for Unity/Mono compatibility)
