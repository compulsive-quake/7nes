# PPU Scrolling

## Overview

Scrolling moves the viewport across a larger virtual background composed of nametables. The PPU can display a 256×240 pixel window into a 512×480 pixel virtual space (with mirroring reducing the unique area to 512×240 or 256×480).

## Scroll Registers

Scrolling is controlled by the PPU's internal registers (v, t, x, w). These are shared between PPUSCROLL ($2005), PPUADDR ($2006), and PPUCTRL ($2000).

### PPUSCROLL ($2005) — Double Write
- **First write** (w=0): Sets X scroll
  - Fine X (bits 0-2) → x register
  - Coarse X (bits 3-7) → t bits 0-4
- **Second write** (w=1): Sets Y scroll
  - Fine Y (bits 0-2) → t bits 12-14
  - Coarse Y (bits 3-7) → t bits 5-9

### PPUCTRL ($2000) — Nametable Select
- Bits 0-1 → t bits 10-11 (nametable select)
- This effectively sets the scroll origin nametable

### How v/t Control Rendering Position
```
v register during rendering:
yyy NN YYYYY XXXXX
 |   |   |     +--- Coarse X: which tile column (0-31)
 |   |   +--------- Coarse Y: which tile row (0-29)
 |   +------------- Nametable: which of 4 nametables
 +----------------- Fine Y: which pixel row within tile (0-7)
```
Fine X is stored separately in the x register (3 bits).

## Per-Scanline Updates

During rendering, the PPU updates v:
1. **Dots 1-256**: Coarse X incremented every 8 dots (with nametable toggle at column 31→0)
2. **Dot 256**: Fine Y / Coarse Y incremented (with nametable toggle at row 29→0)
3. **Dot 257**: Horizontal bits copied from t to v
4. **Dots 280-304 (pre-render only)**: Vertical bits copied from t to v

This means:
- Horizontal scroll is reloaded from t every scanline
- Vertical scroll is reloaded from t only on the pre-render scanline
- Mid-frame writes to PPUSCROLL/PPUCTRL affect t, which affects v on the next reload point

## Nametable Mirroring and Scrolling

### Vertical Mirroring (horizontal scrolling)
- Nametables 0 and 2 share physical VRAM page A
- Nametables 1 and 3 share physical VRAM page B
- Scrolling X wraps between two unique screens

### Horizontal Mirroring (vertical scrolling)
- Nametables 0 and 1 share physical VRAM page A
- Nametables 2 and 3 share physical VRAM page B
- Scrolling Y wraps between two unique screens

### Four-Screen
All four nametables use unique memory (requires extra VRAM on cartridge).

## Screen Splits (Mid-Frame Scroll Changes)

Games like SMB3 use IRQs to change scroll mid-frame:
1. Render status bar with scroll Y=0
2. MMC3 IRQ fires after status bar scanlines
3. IRQ handler changes PPUSCROLL/PPUCTRL for the game area
4. Remaining scanlines render with different scroll values

**Important**: Only horizontal scroll can be cleanly changed mid-frame (via writes to PPUSCROLL and PPUCTRL during HBlank). Vertical scroll changes mid-frame are tricky because fine Y isn't reloaded from t on visible scanlines.

## Scroll Update During VBlank

The safe time to update scroll is during VBlank (scanlines 241-260):
1. Write to PPUCTRL (nametable select)
2. Write to PPUSCROLL (X then Y)
3. These update t, which gets copied to v during pre-render scanline

## CPU VBlank Budget

VBlank lasts ~20 scanlines = ~2,270 CPU cycles. Games must:
- Read controller input
- Compute game logic (partially)
- Update VRAM (nametable writes, palette changes)
- Set up scroll registers
- Set up CHR bank switches
All within this budget, before rendering resumes.
