# NES Emulator Knowledgebase

Reference documentation for NES hardware emulation, derived from "Writing NES Emulator in Rust" by Rafael Bagmanov and supplemented with nesdev wiki details.

## Index

- [CPU (6502)](cpu.md) — Registers, addressing modes, instruction set, interrupt handling
- [PPU (2C02)](ppu.md) — Rendering pipeline, registers, VRAM, nametables, scrolling
- [Bus & Memory Map](bus.md) — CPU/PPU memory maps, mirroring, I/O register routing
- [Cartridges & iNES](cartridges.md) — ROM format, PRG/CHR layout, header parsing
- [Mappers](mappers.md) — Bank switching, IRQ counters, mapper-specific behavior (MMC1, MMC3, etc.)
- [Controllers](controllers.md) — Joypad strobe/shift protocol, button mapping
- [Timing & Synchronization](timing.md) — CPU/PPU clock ratios, catch-up emulation, NMI/IRQ delivery
- [PPU Scrolling](scrolling.md) — Loopy registers, mid-frame scroll splits, nametable mirroring
- [Sprites & OAM](sprites.md) — OAM structure, sprite evaluation, sprite 0 hit, 8x16 sprites
- [Palette & Colors](palette.md) — System palette, attribute tables, color indexing
