# Palette & Colors

## System Palette

The NES PPU has a hardwired palette of 64 colors (some duplicates). The exact colors vary by PPU revision (RP2C02, 2C02G, etc.). Each entry maps to an RGB value.

## Palette RAM

32 bytes of internal PPU memory at $3F00-$3F1F:

| Address | Purpose |
|---------|---------|
| $3F00 | Universal background color |
| $3F01-$3F03 | Background palette 0 |
| $3F05-$3F07 | Background palette 1 |
| $3F09-$3F0B | Background palette 2 |
| $3F0D-$3F0F | Background palette 3 |
| $3F11-$3F13 | Sprite palette 0 |
| $3F15-$3F17 | Sprite palette 1 |
| $3F19-$3F1B | Sprite palette 2 |
| $3F1D-$3F1F | Sprite palette 3 |

### Mirroring
- $3F10 mirrors $3F00 (universal background)
- $3F14 mirrors $3F04
- $3F18 mirrors $3F08
- $3F1C mirrors $3F0C
- $3F20-$3FFF mirrors $3F00-$3F1F

### Color Capacity
- 25 unique colors on screen simultaneously
- Background: 1 universal + 4 palettes × 3 = 13 colors
- Sprites: 4 palettes × 3 = 12 colors (color 0 = transparent)

## Color Indexing

Each pixel has a 2-bit color index from the pattern table:
- **0b00**: Background uses universal background color; sprites are transparent
- **0b01-0b11**: Indexes into the assigned 3-color palette

The full palette address is:
- Background: `$3F00 + palette_num * 4 + color_index`
- Sprite: `$3F10 + palette_num * 4 + color_index`

## Attribute Table (Background Palette Assignment)

The last 64 bytes of each nametable assign palettes to background tiles. Each byte covers a 4×4 tile (32×32 pixel) block:

```
76543210
||||||||
||||||++- Palette for top-left 2×2 tiles
||||++--- Palette for top-right 2×2 tiles
||++----- Palette for bottom-left 2×2 tiles
++------- Palette for bottom-right 2×2 tiles
```

To determine which 2-bit field applies to a specific tile:
```csharp
int shift = 0;
if ((tileColumn & 0x02) != 0) shift += 2;  // right half
if ((tileRow & 0x02) != 0) shift += 4;     // bottom half
int paletteNum = (attrByte >> shift) & 0x03;
```

## Greyscale Mode

When PPUMASK bit 0 is set, all color indices are ANDed with $30, producing only colors from column 0 of the system palette (grey shades).

## Color Emphasis

PPUMASK bits 5-7 can emphasize red, green, or blue channels by darkening the others. This is rarely used.
