# PPU (Picture Processing Unit — 2C02)

## Overview

The PPU is the NES's graphics processor, based on the Ricoh 2C02. It generates a 256×240 pixel composite video signal. The PPU has its own 16 KiB address space, separate from the CPU.

## PPU Memory Map

| Address Range | Size | Description |
|---------------|------|-------------|
| $0000-$0FFF | 4 KiB | Pattern Table 0 (CHR ROM/RAM, "left") |
| $1000-$1FFF | 4 KiB | Pattern Table 1 (CHR ROM/RAM, "right") |
| $2000-$23FF | 1 KiB | Nametable 0 |
| $2400-$27FF | 1 KiB | Nametable 1 |
| $2800-$2BFF | 1 KiB | Nametable 2 (mirror) |
| $2C00-$2FFF | 1 KiB | Nametable 3 (mirror) |
| $3000-$3EFF | | Mirror of $2000-$2EFF |
| $3F00-$3F1F | 32 bytes | Palette RAM |
| $3F20-$3FFF | | Mirrors of palette |

## Registers (CPU-mapped $2000-$2007)

### $2000 — PPUCTRL (write)
```
Bit 7 (V): Generate NMI at start of VBlank
Bit 6 (P): PPU master/slave (unused on NES)
Bit 5 (H): Sprite size (0: 8×8, 1: 8×16)
Bit 4 (B): Background pattern table (0: $0000, 1: $1000)
Bit 3 (S): Sprite pattern table for 8×8 (0: $0000, 1: $1000)
Bit 2 (I): VRAM address increment (0: +1 across, 1: +32 down)
Bits 1-0 (NN): Base nametable ($2000, $2400, $2800, $2C00)
```

### $2001 — PPUMASK (write)
```
Bit 7-5: Color emphasis (BGR)
Bit 4: Show sprites
Bit 3: Show background
Bit 2: Show sprites in leftmost 8 pixels
Bit 1: Show background in leftmost 8 pixels
Bit 0: Greyscale
```

### $2002 — PPUSTATUS (read)
```
Bit 7 (V): VBlank flag — set at scanline 241, cleared on read or at pre-render
Bit 6 (S): Sprite 0 hit — set when opaque bg and sprite 0 pixels overlap
Bit 5 (O): Sprite overflow — set when >8 sprites on a scanline (buggy)
Bits 4-0: Stale PPU bus data
```
**Side effects on read**: Clears VBlank flag, resets write toggle (w).

### $2003 — OAMADDR (write)
Sets OAM address for $2004 access.

### $2004 — OAMDATA (read/write)
Reads/writes OAM at current OAMADDR. Increments OAMADDR on write.

### $2005 — PPUSCROLL (write ×2)
First write: X scroll (fine X to x register, coarse X to t). Second write: Y scroll (fine Y and coarse Y to t). Uses shared write toggle with $2006.

### $2006 — PPUADDR (write ×2)
First write: high byte of VRAM address (to t, bits 8-14). Second write: low byte (to t, then t copied to v). **Note**: High byte first (NOT little-endian).

### $2007 — PPUDATA (read/write)
Reads/writes VRAM at current v address. Auto-increments v by 1 or 32 (per PPUCTRL bit 2). **Important**: Reads from $0000-$3EFF return buffered data (first read is a dummy); palette reads ($3F00+) are immediate.

### $4014 — OAMDMA (write)
Writes a page address (e.g., $02 = $0200). Copies 256 bytes from CPU memory to OAM. Takes 513 CPU cycles (CPU stalled).

## Internal Registers (Loopy)

The PPU has internal registers that control rendering position:

| Register | Bits | Description |
|----------|------|-------------|
| v | 15 | Current VRAM address / scroll position |
| t | 15 | Temporary VRAM address / latch |
| x | 3 | Fine X scroll (pixel offset within tile) |
| w | 1 | Write toggle (shared by $2005/$2006) |

### v/t bit layout
```
yyy NN YYYYY XXXXX
||| || ||||| +++++-- Coarse X scroll (tile column, 0-31)
||| || +++++------- Coarse Y scroll (tile row, 0-29)
||| ++------------- Nametable select (0-3)
+++---------------- Fine Y scroll (pixel row within tile, 0-7)
```

### Register updates during rendering
- **Dot 257**: Horizontal bits copied from t to v
- **Dots 280-304 (pre-render only)**: Vertical bits copied from t to v
- **Every 8 dots**: Coarse X in v incremented
- **Dot 256**: Fine Y / coarse Y in v incremented

## Frame Timing

| Scanline | Description |
|----------|-------------|
| 0-239 | Visible scanlines (rendering) |
| 240 | Post-render (idle) |
| 241 | VBlank begins (NMI triggered at dot 1) |
| 242-260 | VBlank continues |
| 261 | Pre-render scanline (clears flags, resets scroll) |

- **341 PPU cycles (dots) per scanline**
- **262 scanlines per frame**
- **Total: 89,342 PPU cycles/frame**
- **PPU runs at 3× CPU clock speed**

### Key timing points per scanline
- Dot 0: Idle
- Dots 1-256: Pixel output / tile fetching
- Dot 256: Increment scroll Y
- Dot 257: Copy horizontal scroll bits from t to v
- Dots 258-320: Sprite tile fetches (A12 toggles here — important for MMC3)
- Dots 321-336: First two tiles of next scanline prefetched
- Dots 337-340: Dummy fetches

## Rendering Pipeline

Each visible scanline:
1. **Background**: Fetch nametable byte → attribute byte → pattern low → pattern high. Produces 2-bit pixel indices combined with 2-bit palette selection = 4-bit color.
2. **Sprites**: Evaluate which sprites appear on the NEXT scanline (max 8). Fetch their pattern data.
3. **Composition**: Combine background and sprite pixels with priority logic.

## Pattern Tables (Tiles)

Each tile is 8×8 pixels, 16 bytes:
- Bytes 0-7: Bit plane 0 (low bit of each pixel)
- Bytes 8-15: Bit plane 1 (high bit of each pixel)
- Each pixel is 2 bits (4 possible values: transparent/3 colors)
- 512 tiles total (256 per bank)

## Nametables

Each nametable is 1024 bytes:
- 960 bytes: Tile indices (32×30 grid)
- 64 bytes: Attribute table (palette assignments)

### Attribute Table
Each byte controls palettes for a 4×4 tile (32×32 pixel) region:
```
Bits 7-6: Bottom-right 2×2 tiles
Bits 5-4: Bottom-left 2×2 tiles
Bits 3-2: Top-right 2×2 tiles
Bits 1-0: Top-left 2×2 tiles
```
