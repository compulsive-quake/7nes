# CPU (6502 / 2A03)

## Overview

The NES CPU is a Ricoh 2A03, a modified MOS 6502. It lacks decimal mode but includes an on-chip APU. The CPU is 8-bit with a 16-bit address bus (64 KiB addressable space).

## Registers

| Register | Size | Description |
|----------|------|-------------|
| PC | 16-bit | Program Counter — address of next instruction |
| SP | 8-bit | Stack Pointer — offset into $0100-$01FF (grows downward) |
| A | 8-bit | Accumulator — arithmetic/logic results |
| X | 8-bit | Index Register X — loop counters, offsets |
| Y | 8-bit | Index Register Y — similar to X |
| P | 8-bit | Processor Status — 7 flags |

### Status Flags (P register)

```
Bit 7: N (Negative)    — set if result bit 7 is set
Bit 6: V (Overflow)    — set on signed arithmetic overflow
Bit 5: (unused)        — always 1
Bit 4: B (Break)       — set by BRK/PHP, clear by IRQ/NMI
Bit 3: D (Decimal)     — unused on 2A03 (no decimal mode)
Bit 2: I (Interrupt)   — when set, IRQ is ignored (NMI still fires)
Bit 1: Z (Zero)        — set if result is zero
Bit 0: C (Carry)       — set on unsigned overflow/borrow
```

## Addressing Modes

| Mode | Syntax | Size | Description |
|------|--------|------|-------------|
| Immediate | #$nn | 2 | Operand is the byte itself |
| Zero Page | $nn | 2 | Address in first 256 bytes |
| Zero Page,X | $nn,X | 2 | Zero page + X (wraps within page) |
| Zero Page,Y | $nn,Y | 2 | Zero page + Y (wraps within page) |
| Absolute | $nnnn | 3 | Full 16-bit address |
| Absolute,X | $nnnn,X | 3 | Absolute + X (may cross page) |
| Absolute,Y | $nnnn,Y | 3 | Absolute + Y (may cross page) |
| Indirect | ($nnnn) | 3 | JMP only; address at pointer (page-wrap bug) |
| (Indirect,X) | ($nn,X) | 2 | Indexed indirect: pointer at ZP+X |
| (Indirect),Y | ($nn),Y | 2 | Indirect indexed: dereference ZP, add Y |
| Implied | | 1 | No operand |
| Accumulator | A | 1 | Operates on A register |
| Relative | $nn | 2 | Branch offset (signed, -128 to +127) |

### Page Crossing

When an indexed address crosses a page boundary (high byte changes), many instructions take an extra cycle. This is called the "page crossing penalty."

## Instruction Categories

- **Load/Store**: LDA, LDX, LDY, STA, STX, STY
- **Transfer**: TAX, TAY, TXA, TYA, TSX, TXS
- **Arithmetic**: ADC, SBC, INC, DEC, INX, INY, DEX, DEY
- **Logic**: AND, ORA, EOR, BIT
- **Shift/Rotate**: ASL, LSR, ROL, ROR
- **Compare**: CMP, CPX, CPY
- **Branch**: BCC, BCS, BEQ, BNE, BMI, BPL, BVC, BVS
- **Jump/Call**: JMP, JSR, RTS, RTI
- **Stack**: PHA, PLA, PHP, PLP
- **System**: BRK, NOP, SEI, CLI, CLC, SEC, CLD, SED, CLV
- **Unofficial**: ~110 additional opcodes used by many commercial games

### ADC/SBC Notes

ADC is the most complex instruction. The carry flag acts as a 9th bit for multi-byte arithmetic. SBC is implemented as `A - M - (1 - C)` which equals `A + (~M) + C`. The overflow flag detects signed overflow (when adding two positives yields negative, or vice versa).

## Interrupts

### Types
1. **NMI** (Non-Maskable Interrupt) — triggered by PPU entering VBlank. Cannot be disabled by I flag. Vector at $FFFA.
2. **IRQ** (Interrupt Request) — triggered by mappers or APU. Masked when I flag is set. Vector at $FFFE.
3. **BRK** (Software Interrupt) — opcode $00. Uses IRQ vector ($FFFE) but sets B flag.
4. **RESET** — power-on/reset. Vector at $FFFC.

### Interrupt Handling Sequence
1. Finish current instruction
2. Push PC high byte, PC low byte to stack
3. Push status register (with B flag clear for NMI/IRQ, set for BRK)
4. Set I flag (disable further IRQs)
5. Load PC from interrupt vector
6. Takes 7 CPU cycles

### NMI vs IRQ
- NMI is **edge-triggered** — fires once on transition
- IRQ is **level-triggered** — stays asserted as long as source holds it high
- NMI takes priority over IRQ when both pending simultaneously
- CPU checks for interrupts between instructions

## Memory Access Timing

| Operation | Cycles |
|-----------|--------|
| Register-only | 2 |
| Zero page access | 3 |
| Absolute/indexed | 4-7 |
| Page crossing | +1 |
| Branch taken | +1 (+2 if page cross) |

## Little-Endian Byte Order

All 16-bit values in memory are stored little-endian: low byte first, high byte second. Address $8000 is stored as bytes `00 80`.
