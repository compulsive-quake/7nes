# Bus & Memory Map

## CPU Memory Map

| Address Range | Size | Description |
|---------------|------|-------------|
| $0000-$07FF | 2 KiB | Internal RAM |
| $0800-$1FFF | | Mirrors of $0000-$07FF (3 copies) |
| $2000-$2007 | 8 bytes | PPU registers |
| $2008-$3FFF | | Mirrors of $2000-$2007 (every 8 bytes) |
| $4000-$4013 | | APU registers |
| $4014 | | OAM DMA |
| $4015 | | APU status |
| $4016 | | Controller 1 |
| $4017 | | Controller 2 / APU frame counter |
| $4018-$401F | | Normally unused |
| $4020-$5FFF | | Cartridge expansion area |
| $6000-$7FFF | 8 KiB | Cartridge PRG RAM (battery-backed save) |
| $8000-$BFFF | 16 KiB | Cartridge PRG ROM (lower bank) |
| $C000-$FFFF | 16 KiB | Cartridge PRG ROM (upper bank) |

### Special Addresses
- **$FFFA-$FFFB**: NMI vector
- **$FFFC-$FFFD**: Reset vector
- **$FFFE-$FFFF**: IRQ/BRK vector

### RAM Mirroring
CPU RAM is 2 KiB but mapped to a 8 KiB range. Only 11 address bits matter:
```
Effective address = addr & 0x07FF
```
This is because the NES motherboard only has 11 address traces from CPU to RAM.

### PPU Register Mirroring
8 PPU registers mirror across $2000-$3FFF:
```
Effective register = addr & 0x2007
(or: addr & 0b0010_0000_0000_0111)
```

## Bus Architecture

The bus connects CPU, PPU, APU, and cartridge:
- **Address bus** (16-bit): carries target address
- **Data bus** (8-bit): carries data byte
- **Control lines**: read/write signal

The bus handles:
1. Memory-mapped I/O routing (PPU regs, APU, controllers)
2. RAM mirroring
3. Cartridge mapper delegation
4. DMA coordination
5. Interrupt propagation (NMI from PPU, IRQ from mapper/APU)
