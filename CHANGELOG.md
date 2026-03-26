# Changelog

## [1.0.3] - 2026-03-25

### Added
- Player hand/item hiding when using the NES TV — arms, body, and held items (guns, tools) are hidden while playing, viewing controls, or calibrating, and restored when closing

### Changed
- Increased controls hint font size from 12 to 18 and hint bar height from 25 to 30 for better readability

## [1.0.2] - 2026-03-25

### Changed
- Converted nesConsole from Workstation to SecureLoot block class with a 1-slot loot container for cartridges
- Restyled nesConsole window to match the game's power source panel aesthetic (dark grey background, black border)
- nesConsole window group now uses XUiC_LootWindowGroup with LootContainer grid controller instead of WorkstationToolGrid
- Cartridge detection (CartridgeHelper) reads from TileEntityLootContainer.items instead of TileEntityWorkstation.Tools

### Fixed
- Eliminated NullReferenceException crashes caused by XUiC_WorkstationWindowGroup requiring a recipe list that nesConsole doesn't have
- Fixed XUi style key errors (medGrey, nearWhite) in nesConsole info panel

### Removed
- Removed workstation recipe list suppression Harmony patch (RecipeListSelectedEntryPatch) — no longer needed

## [1.0.1] - 2026-03-25

### Added
- 9 new mappers: MMC3/Mapper 4 (SMB2, SMB3, Mega Man 3-6), AxROM/Mapper 7 (Battletoads), MMC2/Mapper 9 (Punch-Out!!), MMC4/Mapper 10, Color Dreams/Mapper 11, BNROM/Mapper 34, GxROM/Mapper 66, Sunsoft FME-7/Mapper 69 (Gimmick!), Camerica/Mapper 71 (Micro Machines)
- Mapper scanline IRQ support (NotifyScanline on IMapper interface, PPU scanline counter notification, cartridge IrqPending flag)
- No-signal screen calibration mode (numpad controls to adjust position/size of idle screen)
- ROM load error notifications shown on-screen when a cartridge fails to load
- NES Console workstation info panel with setup instructions replacing the empty crafting info panel
- `/done` Claude Code skill for changelog + version bump + commit + push

### Changed
- CartridgeHandAdjuster simplified: removed debug UI and transform search/adjust logic, replaced with hand-hiding approach that disables SkinnedMeshRenderers when holding a cartridge
- No-signal screen dimensions now use adjustable fields instead of hardcoded constants

### Fixed
- Resolved merge conflicts in NESConsolePrefab.prefab and SetupNESPrefab.cs editor script
- Rebuilt nescartridge.unity3d asset bundle with clean prefab
