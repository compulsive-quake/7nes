# Timing & Synchronization

## Clock Ratios (NTSC)

| Component | Clock Speed | Ratio |
|-----------|-------------|-------|
| Master clock | 21.477 MHz | 1× |
| CPU | 1.789 MHz | master ÷ 12 |
| PPU | 5.369 MHz | master ÷ 4 |
| APU | ~894.9 KHz | CPU ÷ 2 |

**Key ratio: 3 PPU cycles = 1 CPU cycle**

## Frame Timing

- 341 PPU dots per scanline
- 262 scanlines per frame
- 89,342 PPU cycles per frame
- ~29,780 CPU cycles per frame
- ~60 FPS (NTSC)

## Catch-Up Emulation

The standard approach for emulating multiple independent clocks in a single thread:

1. Execute one CPU instruction (N cycles)
2. Run PPU for N × 3 cycles
3. Run APU for N cycles (or N ÷ 2 for half-rate)
4. Propagate interrupt signals
5. Repeat

This is simpler than cycle-accurate interleaving but introduces timing granularity equal to one CPU instruction (2-7 cycles).

### Timing Implications
- An IRQ triggered mid-instruction won't be serviced until the instruction completes
- This is correct behavior (real CPU checks interrupts between instructions)
- But the batch execution means PPU events within a single CPU instruction are all processed before the CPU can react

## NMI Delivery

1. PPU enters VBlank (scanline 241, dot 1)
2. PPU sets VBlank flag in PPUSTATUS
3. If PPUCTRL bit 7 is set, PPU signals NMI
4. CPU detects NMI at start of next instruction
5. CPU pushes PC and status, loads from $FFFA

### NMI Edge Cases
- Reading PPUSTATUS on the exact dot VBlank is set can suppress the NMI
- Writing PPUCTRL to enable NMI while already in VBlank triggers NMI immediately

## IRQ Delivery

1. Source asserts IRQ line (mapper counter reaches 0, APU frame counter, etc.)
2. Bus propagates to CPU.IrqPending
3. CPU checks at start of next instruction
4. If I flag is clear, CPU services IRQ (push PC/status, load from $FFFE)
5. IRQ remains asserted until source deasserts (level-triggered)

### IRQ Acknowledgment
Unlike NMI (edge-triggered, auto-cleared), IRQ must be explicitly acknowledged by clearing the source:
- MMC3: Write to $E000
- APU: Read $4015 or set frame counter mode

## DMA Timing

OAM DMA ($4014 write) stalls the CPU for 513 cycles:
- 1 dummy cycle (2 if on odd CPU cycle)
- 256 read cycles + 256 write cycles
- PPU and APU continue running during DMA
- Interrupts that fire during DMA are serviced after DMA completes

## Mapper Scanline Counting

For mappers with scanline counters (MMC3), the PPU notifies the mapper at a specific cycle each scanline. The standard approximation is **dot 260**, which corresponds to when the PPU begins fetching sprite tile data and A12 transitions.

Clocking occurs on:
- Visible scanlines (0-239)
- Pre-render scanline (261)
- Only when rendering is enabled (PPUMASK bits 3-4)
