# Mappers

## Overview

Mappers are cartridge hardware that extends the NES's limited address space through bank switching. The NES can only address 32 KiB of PRG ROM and 8 KiB of CHR ROM directly. Mappers swap banks of ROM in and out of these windows.

## Common Mappers

### Mapper 0 — NROM
No bank switching. PRG: 16 or 32 KiB fixed. CHR: 8 KiB fixed.
Games: Super Mario Bros, Donkey Kong, Ice Climber.

### Mapper 1 — MMC1 (SxROM)
Serial shift register interface. Supports PRG banking (16/32 KiB modes), CHR banking (4/8 KiB modes), and mirroring control.
Games: Legend of Zelda, Metroid, Mega Man 2.

### Mapper 2 — UxROM
Simple PRG bank switching. 16 KiB switchable bank at $8000, fixed last bank at $C000. No CHR banking.
Games: Mega Man, Castlevania, Contra.

### Mapper 3 — CNROM
Simple CHR bank switching. Entire 8 KiB CHR ROM bank selected by writing to $8000-$FFFF. PRG is fixed.
Games: Gradius, Solomon's Key.

### Mapper 4 — MMC3 (TxROM)
The most complex common mapper. Fine-grained banking and scanline-based IRQ counter.
Games: Super Mario Bros. 2, Super Mario Bros. 3, Mega Man 3-6, Kirby's Adventure.

### Mapper 7 — AxROM
32 KiB PRG bank switching, single-screen mirroring control.
Games: Battletoads, Marble Madness.

## MMC3 (Mapper 4) — Detailed

Source: kevtris.org hardware testing (all MMC3 revisions: A, B, C, and pirate "88" conform).

### Banking

- **PRG**: Two switchable 8 KiB banks + two fixed 8 KiB banks
- **CHR**: Six switchable banks (two 2 KiB + four 1 KiB)
- **Mirroring**: Software-controllable horizontal/vertical

### Register Map

Chip uses A0, A13, A14, A15 for decoding. 8 registers, each 8 bits wide.

| Address | Even/Odd | Function |
|---------|----------|----------|
| $8000 | Even | Bank select (control register) |
| $8001 | Odd | Bank data (writes to register selected by $8000) |
| $A000 | Even | Mirroring (bit 0: 0=vertical, 1=horizontal) |
| $A001 | Odd | WRAM protect (bit 7=enable WRAM, bit 6=write-protect) |
| $C000 | Even | IRQ latch value |
| $C001 | Odd | IRQ counter clear (forces reload on next A12 edge) |
| $E000 | Even | IRQ disable + acknowledge (clears IRQ flag) |
| $E001 | Odd | IRQ enable |

### Bank Select ($8000)
```
7  bit  0
---------
CSxx xMMM

C: CHR A12 inversion. XORs $1000 with CHR addresses.
S: PRG ROM swapping control.
   0 = $8000/$A000 switchable, $C000/$E000 fixed
   1 = $A000/$C000 switchable, $8000/$E000 fixed
M: Register select (R0-R7)
```

### Bank Data Registers ($8001 — R0-R7)

| R# | Mode bits | Function |
|----|-----------|----------|
| R0 | 000 | Select 2 consecutive 1K CHR pages at $0000 (low bit of value ignored) |
| R1 | 001 | Select 2 consecutive 1K CHR pages at $0800 (low bit of value ignored) |
| R2 | 010 | Select 1K CHR page at $1000 |
| R3 | 011 | Select 1K CHR page at $1400 |
| R4 | 100 | Select 1K CHR page at $1800 |
| R5 | 101 | Select 1K CHR page at $1C00 |
| R6 | 110 | Select 8K PRG page at $8000 or $C000 |
| R7 | 111 | Select 8K PRG page at $A000 |

Note: CHR addresses above are before A12 inversion (C bit).

### PRG Banking Modes

$E000-$FFFF is ALWAYS fixed to the last bank of ROM.

- **Mode 0** (S=0): $8000=R6, $A000=R7, $C000=second-to-last, $E000=last
- **Mode 1** (S=1): $8000=second-to-last, $A000=R7, $C000=R6, $E000=last

### CHR A12 Inversion

When C bit is set, CHR addresses are XORed with $1000:
- 2 KiB banks (R0, R1) map to $1000-$1FFF instead of $0000-$0FFF
- 1 KiB banks (R2-R5) map to $0000-$0FFF instead of $1000-$1FFF

This controls which pattern table is used for background vs sprites, which affects A12 transitions for the IRQ counter.

### IRQ Counter (from kevtris hardware testing)

The MMC3 watches A12 on the PPU bus and decrements the counter on every RISING EDGE.

#### Key Hardware Facts (all revisions conform)

1. **Counter never stops** — continues decrementing and reloading as long as A12 toggles
2. **No direct access** to the IRQ counter register
3. **$C000**: Sets the reload value (latch). Does NOT affect the running counter. Value is only checked at the instant of reloading (on the A12 rising edge).
4. **$C001**: Clears the IRQ counter to 0. The latch value ($C000) will be copied into the counter on the NEXT A12 rising edge. Value written is irrelevant.
5. **$E000**: Disables the IRQ flag flip-flop AND resets (acknowledges) it. Counter is unaffected.
6. **$E001**: Enables the IRQ flag flip-flop. Counter is unaffected.
7. **IRQ fires when counter transitions from non-zero to 0** (if enabled).
8. **Scanline count = N+1** where N = latch value. Supports 2 to 256 scanlines.
9. **Latch = 0** produces a SINGLE interrupt on the next A12 edge. No more until latch changes to non-zero (counter reloads to 0, doesn't re-fire because no non-zero → zero transition).
10. **Counter requires rendering** — will NOT decrement unless bit 3 OR bit 4 of PPUCTRL ($2000) are set (i.e., at least one pattern table at $1000 so A12 toggles).

#### A12 Deglitch Filter

All MMC3 carts have a 220pF capacitor from ground to CHR A12 to deglitch the signal for the IRQ counter. In emulation, the fixed dot-260 approximation inherently avoids glitch issues.

#### Counter Clocking Behavior (Accurate)

```
old_counter = counter
if (counter == 0 || reload_flag):
    counter = latch_value
else:
    counter -= 1

if counter == 0 AND irq_enabled AND (old_counter != 0 || reload_flag):
    set IRQ pending

reload_flag = false
```

#### Hardware Test Results (kevtris)

Test: $C000=02, toggle A12 3 times → IRQ fires (count: 3 = N+1 = 2+1).
Then: clear IRQ, toggle A12 twice, write $C000=03, write $C001, write $C000=04, toggle A12 → takes **5** counts to fire. This proves $C000's value is only read at reload time (used 04, not 03).

#### Reload Timing for Games

Some games (Mega Man 6, Pinbot) rely on the fact that writing $E000 (ack), $E001 (re-enable), then $C000 (new period) is legal as long as $C000 is written before the next scanline. The counter reloads from $C000 on the NEXT A12 edge after reaching 0.

### WRAM Control ($A001)

```
Bit 7 (W): 0=disable WRAM (unmapped/open bus), 1=enable WRAM at $6000-$7FFF
Bit 6 (R): 0=WRAM writable, 1=WRAM read-only
```

### MMC3 Board Variants

| Board | Max PRG | Max CHR | WRAM | Notes |
|-------|---------|---------|------|-------|
| TEROM | 64K | 64K | - | Standard, mirroring can be hardwired |
| TFROM | 512K | 64K | - | Standard |
| TGROM | 512K | VRAM | - | Uses 8K CHR RAM |
| TKROM | 512K | 256K | 8K battery | Kirby's Adventure |
| TLROM | 512K | 256K | - | Most common board |
| TSROM | 512K | 256K | 8K | SMB3 uses this board |
| TQROM | 128K | 64K+8K RAM | - | Both CHR ROM and CHR RAM, A16 selects |
| TVROM | 128K | 64K | 4K NT RAM | 4-screen mirroring (Rad Racer II) |

### SMB3-Specific Behavior

SMB3 (NES-TSROM board) uses the MMC3 IRQ to split the screen:
1. Status bar at top (~27 scanlines) with its own CHR banks and scroll=0
2. IRQ fires after status bar, handler switches CHR banks AND scroll for the game area
3. Timing accuracy is critical — the handler must complete before the next scanline's visible dots begin
4. SMB3 uses CHR A12 inversion (C=1), putting 2KB banks at $1000 (sprites) and 1KB banks at $0000 (background). The IRQ handler changes the 1KB background banks to swap tilesets between status bar and game area.

#### Common Emulation Issues with SMB3
- **Off-by-one in scanline count**: Must clock pre-render scanline (261) to properly initialize counter
- **Re-firing when latch=0**: Must guard against repeated IRQ when counter stays at 0
- **IRQ latency**: CPU can't service IRQ until current instruction completes (7-14 cycle delay from event to first handler instruction)
- **CHR bank timing at split**: Sprites evaluated early (before IRQ) use old banks; sprites on scanlines after IRQ use new banks
