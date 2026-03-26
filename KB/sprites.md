# Sprites & OAM

## Object Attribute Memory (OAM)

256 bytes of internal PPU memory. Stores data for 64 sprites, 4 bytes each.

### OAM Entry Format

| Byte | Description |
|------|-------------|
| 0 | Y position (top of sprite, actual display is Y+1) |
| 1 | Tile index number |
| 2 | Attributes |
| 3 | X position |

### Attribute Byte (Byte 2)
```
Bit 7: Flip vertically
Bit 6: Flip horizontally
Bit 5: Priority (0: in front of background, 1: behind background)
Bits 1-0: Palette number (selects from sprite palettes 4-7)
```

## Sprite Sizes

Controlled by PPUCTRL bit 5:
- **8×8 sprites**: Tile index selects from pattern table chosen by PPUCTRL bit 3
- **8×16 sprites**: Bit 0 of tile index selects pattern table ($0000 or $1000). Actual tile number is the even tile (bit 0 masked off); the odd tile is the bottom half.

### 8×16 Sprite Tile Layout
```
Tile index & 0xFE = top half
Tile index | 0x01 = bottom half
Pattern table = (tile index & 0x01) ? $1000 : $0000
```

## Sprite Evaluation

Each scanline, the PPU evaluates which sprites appear on the NEXT scanline:
1. Scan all 64 OAM entries
2. Find sprites whose Y range overlaps the scanline
3. Take the first 8 (lower index = higher priority)
4. If more than 8 found, set sprite overflow flag (buggy on real hardware)

## Sprite Priority

- Among sprites: **Lower OAM index = higher priority** (sprite 0 is highest)
- Between sprite and background:
  - Sprite priority bit 0 (front): sprite wins over opaque background
  - Sprite priority bit 1 (behind): opaque background wins over sprite

## Sprite 0 Hit

A flag set in PPUSTATUS when:
1. An opaque pixel of sprite 0 overlaps an opaque background pixel
2. Both background and sprite rendering are enabled
3. The pixel is not at x=255
4. Not in the leftmost 8 pixels unless both left-column masks are enabled

Games poll this flag to detect when the PPU has reached a specific screen position, enabling mid-frame effects like scroll splits.

**Timing**: The hit is detected at the exact pixel position during rendering. The flag is cleared at the start of the pre-render scanline.

## OAM DMA

Writing to $4014 triggers a fast 256-byte copy from CPU memory to OAM:
- Write value = source page (e.g., $02 means copy from $0200-$02FF)
- Takes 513 CPU cycles (CPU is stalled)
- Copies starting at current OAMADDR
- Most games set OAMADDR to 0 before DMA

## Sprite Rendering

For each visible sprite on the current scanline:
1. Compute which row of the sprite is visible (considering Y position and vertical flip)
2. Fetch pattern data for that row from the appropriate pattern table
3. Apply horizontal flip if needed
4. Combine with palette to produce colored pixels
5. Output to a sprite line buffer

The sprite line buffer is then composited with the background during final pixel output.

## Y Position Quirk

The sprite Y value in OAM represents the scanline ABOVE where the sprite starts displaying. So a sprite at Y=0 actually appears on scanline 1. This means sprites cannot appear on scanline 0.

To hide a sprite, set Y to $EF (239) or higher so it falls off the bottom of the visible area.
