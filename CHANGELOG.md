# Changelog

## [1.0.7] - 2026-03-27

### Changed
- Calibrated all 4 rotations for both small and large NES TV screen positioning
- Numpad calibration now uses two-speed movement: fine step (0.001) on tap, fast step (0.01) after holding 1 second
- Holding numpad keys continuously moves the image instead of requiring repeated presses

## [1.0.6] - 2026-03-26

### Added
- Quick-insert cartridge: left-click a nesConsole block while holding a cartridge to insert it directly without opening the inventory, swapping out any existing cartridge

## [1.0.5] - 2026-03-26

### Added
- NES emulator knowledgebase (KB/) with 10 reference documents covering CPU, PPU, bus, cartridges, mappers, controllers, timing, scrolling, sprites, and palette
- Fullscreen overlay mode — press U (rebindable) to view the NES screen fullscreen while playing
- Fullscreen key binding in the controls dialog under a new "System" section

### Fixed
- MMC3 (Mapper 4) IRQ counter: implemented accurate "alternate" behavior with transition-to-zero guard, preventing spurious re-firing when latch is 0
- MMC3 $C001 write now clears the IRQ counter immediately (matches kevtris hardware testing)
- MMC3 IRQ propagation during OAM DMA — CPU now sees mapper IRQs as soon as DMA completes
- Fullscreen toggle key (U) now detected via OnGUI Event.current instead of Input.GetKeyDown, preventing 7DTD's input system from consuming the keypress

### Changed
- PPU sprite evaluation refactored to parameterized buffer method (EvaluateSpriteBuffers)
- Controls hint bar now shows the fullscreen key binding

## [1.0.4] - 2026-03-26

### Fixed
- Sprite Y-offset: sprites now render 1 scanline lower (matching real NES hardware OAM Y+1 behavior), fixing face/body artifacts on Mario and other characters
- IRQ propagation: IRQ line is now properly level-triggered (de-asserts when source clears), fixing edge cases in MMC3 IRQ handling

### Changed
- System palette replaced with hardware-measured 2C02G values from nesdev.org/wiki/PPU_palettes
- Framebuffer output now applies sRGB-to-linear conversion to compensate for Unity's linear rendering pipeline gamma correction
- Added overscan cropping (8px from each edge) with nearest-neighbor scaling for cleaner screen edges
- Controls dialog now consumes keyboard/gamepad events to prevent input bleed-through to the game

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
