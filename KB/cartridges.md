# Cartridges & iNES Format

## Physical Cartridge

NES cartridges contain:
- **PRG ROM**: Program code (connected to CPU bus)
- **CHR ROM**: Graphics data (connected to PPU bus). Some carts use CHR RAM instead.
- **Mapper hardware**: Bank-switching logic for games exceeding base address space
- **Optional PRG RAM**: Battery-backed save RAM (e.g., Zelda)

## iNES File Format

The standard ROM dump format (designed by Marat Fayzullin).

### Header (16 bytes)

| Offset | Size | Description |
|--------|------|-------------|
| 0-3 | 4 | Magic: `NES\x1A` |
| 4 | 1 | PRG ROM size in 16 KiB units |
| 5 | 1 | CHR ROM size in 8 KiB units (0 = CHR RAM) |
| 6 | 1 | Flags 6 |
| 7 | 1 | Flags 7 |
| 8-15 | 8 | Padding (should be zero) |

### Flags 6
```
Bit 0: Mirroring (0: horizontal, 1: vertical)
Bit 1: Battery-backed PRG RAM at $6000-$7FFF
Bit 2: 512-byte trainer at $7000-$71FF (before PRG ROM)
Bit 3: Four-screen VRAM (ignore mirroring bit)
Bits 4-7: Lower nibble of mapper number
```

### Flags 7
```
Bits 4-7: Upper nibble of mapper number
Bits 2-3: iNES format version (if == 2, this is NES 2.0)
Bits 0-1: VS/Playchoice
```

### Mapper Number
```csharp
byte mapper = (flags7 & 0xF0) | (flags6 >> 4);
```

### Layout After Header
1. Optional 512-byte trainer (if flags6 bit 2 set)
2. PRG ROM data (flags[4] × 16384 bytes)
3. CHR ROM data (flags[5] × 8192 bytes)

### PRG ROM Size Handling
If PRG ROM is 16 KiB (1 bank), it occupies $8000-$BFFF. The $C000-$FFFF range should mirror it:
```csharp
if (prgRom.Length == 0x4000 && addr >= 0x4000)
    addr = addr % 0x4000;
```

### Mirroring
```csharp
Mirroring mirroring;
if (fourScreen)      mirroring = FourScreen;
else if (verticalBit) mirroring = Vertical;
else                  mirroring = Horizontal;
```
Mappers can override mirroring at runtime (e.g., MMC1, MMC3).
