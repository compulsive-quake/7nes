using System;

namespace SevenNes.Core
{
    public class Ppu
    {
        private readonly Nes _nes;

        // Memory
        public byte[] Vram = new byte[2048];
        public byte[] Oam = new byte[256];
        public byte[] Palette = new byte[32];
        public byte[] OamSecondary = new byte[32];

        // Framebuffer (RGBA)
        public byte[] FrameBuffer = new byte[256 * 240 * 4];

        // Registers
        private byte _ctrl;       // $2000 PPUCTRL
        private byte _mask;       // $2001 PPUMASK
        private byte _status;     // $2002 PPUSTATUS
        private byte _oamAddr;    // $2003 OAMADDR

        // Internal registers
        private ushort _v;        // Current VRAM address (15 bits)
        private ushort _t;        // Temporary VRAM address (15 bits)
        private byte _x;          // Fine X scroll (3 bits)
        private bool _w;          // Write toggle
        private byte _dataBuffer; // PPUDATA read buffer

        // Rendering state
        public int Scanline;
        public int Cycle;
        public bool FrameComplete;
        private bool _nmiTriggered;
        private long _frameCount;

        // NES system palette (64 colors, RGB)
        private static readonly byte[,] SystemPalette = new byte[64, 3]
        {
            { 84, 84, 84 },    // 0x00
            { 0, 30, 116 },    // 0x01
            { 8, 16, 144 },    // 0x02
            { 48, 0, 136 },    // 0x03
            { 68, 0, 100 },    // 0x04
            { 92, 0, 48 },     // 0x05
            { 84, 4, 0 },      // 0x06
            { 60, 24, 0 },     // 0x07
            { 32, 42, 0 },     // 0x08
            { 8, 58, 0 },      // 0x09
            { 0, 64, 0 },      // 0x0A
            { 0, 60, 0 },      // 0x0B
            { 0, 50, 60 },     // 0x0C
            { 0, 0, 0 },       // 0x0D
            { 0, 0, 0 },       // 0x0E
            { 0, 0, 0 },       // 0x0F
            { 152, 150, 152 }, // 0x10
            { 8, 76, 196 },    // 0x11
            { 48, 50, 236 },   // 0x12
            { 92, 30, 228 },   // 0x13
            { 136, 20, 176 },  // 0x14
            { 160, 20, 100 },  // 0x15
            { 152, 34, 32 },   // 0x16
            { 120, 60, 0 },    // 0x17
            { 84, 90, 0 },     // 0x18
            { 40, 114, 0 },    // 0x19
            { 8, 124, 0 },     // 0x1A
            { 0, 118, 40 },    // 0x1B
            { 0, 102, 120 },   // 0x1C
            { 0, 0, 0 },       // 0x1D
            { 0, 0, 0 },       // 0x1E
            { 0, 0, 0 },       // 0x1F
            { 236, 238, 236 }, // 0x20
            { 76, 154, 236 },  // 0x21
            { 120, 124, 236 }, // 0x22
            { 176, 98, 236 },  // 0x23
            { 228, 84, 236 },  // 0x24
            { 236, 88, 180 },  // 0x25
            { 236, 106, 100 }, // 0x26
            { 212, 136, 32 },  // 0x27
            { 160, 170, 0 },   // 0x28
            { 116, 196, 0 },   // 0x29
            { 76, 208, 32 },   // 0x2A
            { 56, 204, 108 },  // 0x2B
            { 56, 180, 204 },  // 0x2C
            { 60, 60, 60 },    // 0x2D
            { 0, 0, 0 },       // 0x2E
            { 0, 0, 0 },       // 0x2F
            { 236, 238, 236 }, // 0x30
            { 168, 204, 236 }, // 0x31
            { 188, 188, 236 }, // 0x32
            { 212, 178, 236 }, // 0x33
            { 236, 174, 236 }, // 0x34
            { 236, 174, 212 }, // 0x35
            { 236, 180, 176 }, // 0x36
            { 228, 196, 144 }, // 0x37
            { 204, 210, 120 }, // 0x38
            { 180, 222, 120 }, // 0x39
            { 168, 226, 144 }, // 0x3A
            { 152, 226, 180 }, // 0x3B
            { 160, 214, 228 }, // 0x3C
            { 160, 162, 160 }, // 0x3D
            { 0, 0, 0 },       // 0x3E
            { 0, 0, 0 },       // 0x3F
        };

        public Ppu(Nes nes)
        {
            _nes = nes;
        }

        public void Reset()
        {
            _ctrl = 0;
            _mask = 0;
            _status = 0;
            _oamAddr = 0;
            _v = 0;
            _t = 0;
            _x = 0;
            _w = false;
            _dataBuffer = 0;
            Scanline = 0;
            Cycle = 0;
            FrameComplete = false;
            _nmiTriggered = false;
            _frameCount = 0;

            Array.Clear(Vram, 0, Vram.Length);
            Array.Clear(Oam, 0, Oam.Length);
            Array.Clear(Palette, 0, Palette.Length);
            Array.Clear(OamSecondary, 0, OamSecondary.Length);
            Array.Clear(FrameBuffer, 0, FrameBuffer.Length);
        }

        public byte ReadRegister(ushort address)
        {
            ushort reg = (ushort)(address & 0x0007);
            byte result = 0;

            switch (reg)
            {
                case 0: // PPUCTRL - write only
                    break;
                case 1: // PPUMASK - write only
                    break;
                case 2: // PPUSTATUS
                    result = (byte)((_status & 0xE0) | (_dataBuffer & 0x1F));
                    _status &= 0x7F; // Clear vblank flag
                    _w = false;      // Reset write toggle
                    break;
                case 3: // OAMADDR - write only
                    break;
                case 4: // OAMDATA
                    result = Oam[_oamAddr];
                    break;
                case 5: // PPUSCROLL - write only
                    break;
                case 6: // PPUADDR - write only
                    break;
                case 7: // PPUDATA
                    result = _dataBuffer;
                    _dataBuffer = PpuRead(_v);

                    // Palette reads are not buffered
                    if (_v >= 0x3F00)
                    {
                        result = _dataBuffer;
                        // Buffer gets value from "underneath" the palette (nametable mirror)
                        _dataBuffer = PpuRead((ushort)(_v - 0x1000));
                    }

                    _v += (ushort)((_ctrl & 0x04) != 0 ? 32 : 1);
                    break;
            }

            return result;
        }

        public void WriteRegister(ushort address, byte value)
        {
            ushort reg = (ushort)(address & 0x0007);

            switch (reg)
            {
                case 0: // PPUCTRL
                    _ctrl = value;
                    // Nametable select bits go to t bits 10-11
                    _t = (ushort)((_t & 0xF3FF) | ((value & 0x03) << 10));
                    break;
                case 1: // PPUMASK
                    _mask = value;
                    break;
                case 2: // PPUSTATUS - read only
                    break;
                case 3: // OAMADDR
                    _oamAddr = value;
                    break;
                case 4: // OAMDATA
                    Oam[_oamAddr] = value;
                    _oamAddr++;
                    break;
                case 5: // PPUSCROLL
                    if (!_w)
                    {
                        // First write: X scroll
                        _x = (byte)(value & 0x07);
                        _t = (ushort)((_t & 0xFFE0) | (value >> 3));
                        _w = true;
                    }
                    else
                    {
                        // Second write: Y scroll
                        _t = (ushort)((_t & 0x8C1F) | ((value & 0x07) << 12) | ((value >> 3) << 5));
                        _w = false;
                    }
                    break;
                case 6: // PPUADDR
                    if (!_w)
                    {
                        // First write: high byte
                        _t = (ushort)((_t & 0x00FF) | ((value & 0x3F) << 8));
                        _w = true;
                    }
                    else
                    {
                        // Second write: low byte
                        _t = (ushort)((_t & 0xFF00) | value);
                        _v = _t;
                        _w = false;
                    }
                    break;
                case 7: // PPUDATA
                    PpuWrite(_v, value);
                    _v += (ushort)((_ctrl & 0x04) != 0 ? 32 : 1);
                    break;
            }
        }

        public byte PpuRead(ushort addr)
        {
            addr &= 0x3FFF;

            if (addr < 0x2000)
            {
                // Pattern tables - read from cartridge
                return _nes.Cartridge.Mapper.PpuRead(addr);
            }
            else if (addr < 0x3F00)
            {
                // Nametables
                return Vram[MirrorNametableAddress(addr) & 0x07FF];
            }
            else
            {
                // Palette
                ushort palAddr = (ushort)(addr & 0x1F);
                // Mirrors of $3F10/$3F14/$3F18/$3F1C are $3F00/$3F04/$3F08/$3F0C
                if (palAddr >= 16 && (palAddr & 0x03) == 0)
                    palAddr -= 16;
                return Palette[palAddr];
            }
        }

        public void PpuWrite(ushort addr, byte value)
        {
            addr &= 0x3FFF;

            if (addr < 0x2000)
            {
                // Pattern tables - write to cartridge (usually CHR RAM)
                _nes.Cartridge.Mapper.PpuWrite(addr, value);
            }
            else if (addr < 0x3F00)
            {
                // Nametables
                Vram[MirrorNametableAddress(addr) & 0x07FF] = value;
            }
            else
            {
                // Palette
                ushort palAddr = (ushort)(addr & 0x1F);
                if (palAddr >= 16 && (palAddr & 0x03) == 0)
                    palAddr -= 16;
                Palette[palAddr] = value;
            }
        }

        private ushort MirrorNametableAddress(ushort addr)
        {
            addr = (ushort)((addr - 0x2000) & 0x0FFF);
            int table = addr / 0x0400;
            int offset = addr & 0x03FF;

            int mirrorMode = _nes.Cartridge != null ? _nes.Cartridge.MirrorMode : 1;

            switch (mirrorMode)
            {
                case 0: // Horizontal
                    // Tables 0,1 -> 0; Tables 2,3 -> 1
                    if (table == 0 || table == 1)
                        return (ushort)offset;
                    else
                        return (ushort)(0x0400 + offset);

                case 1: // Vertical
                    // Tables 0,2 -> 0; Tables 1,3 -> 1
                    if (table == 0 || table == 2)
                        return (ushort)offset;
                    else
                        return (ushort)(0x0400 + offset);

                case 2: // Single screen lower
                    return (ushort)offset;

                case 3: // Single screen upper
                    return (ushort)(0x0400 + offset);

                case 4: // Four screen (uses all VRAM)
                    return (ushort)(table * 0x0400 + offset);

                default:
                    return (ushort)offset;
            }
        }

        private bool IsRenderingEnabled()
        {
            return (_mask & 0x18) != 0; // Show bg or show sprites
        }

        public void Step()
        {
            // Visible scanlines: render at cycle 0 of each scanline (scanline-based renderer)
            if (Scanline >= 0 && Scanline <= 239 && Cycle == 0)
            {
                if (IsRenderingEnabled())
                {
                    RenderScanline(Scanline);
                }
            }

            // Pre-render scanline (261)
            if (Scanline == 261)
            {
                if (Cycle == 1)
                {
                    // Clear vblank, sprite 0 hit, overflow
                    _status &= 0x1F;
                    _nmiTriggered = false;
                }

                if (Cycle >= 280 && Cycle <= 304 && IsRenderingEnabled())
                {
                    // Copy vertical bits from t to v
                    _v = (ushort)((_v & 0x041F) | (_t & 0x7BE0));
                }
            }

            // VBlank scanline (241)
            if (Scanline == 241 && Cycle == 1)
            {
                _status |= 0x80; // Set vblank flag
                if ((_ctrl & 0x80) != 0 && !_nmiTriggered)
                {
                    _nmiTriggered = true;
                    _nes.Cpu.NmiPending = true;
                }
            }

            // Advance cycle and scanline
            Cycle++;
            if (Cycle > 340)
            {
                Cycle = 0;
                Scanline++;
                if (Scanline > 261)
                {
                    Scanline = 0;
                    FrameComplete = true;
                    _frameCount++;
                }
            }
        }

        private void RenderScanline(int scanline)
        {
            bool showBg = (_mask & 0x08) != 0;
            bool showSprites = (_mask & 0x10) != 0;
            bool showBgLeft = (_mask & 0x02) != 0;
            bool showSpritesLeft = (_mask & 0x04) != 0;

            // Background pixel data for this scanline (palette index, 0 = transparent)
            byte[] bgPixels = new byte[256];
            // Sprite pixel data
            byte[] sprPixels = new byte[256];
            byte[] sprPriority = new byte[256]; // 0 = in front of bg, 1 = behind bg
            bool[] sprIsSprite0 = new bool[256];

            // Render background
            if (showBg)
            {
                RenderBackgroundScanline(scanline, bgPixels, showBgLeft);
            }

            // Render sprites
            if (showSprites)
            {
                RenderSpriteScanline(scanline, sprPixels, sprPriority, sprIsSprite0, showSpritesLeft);
            }

            // Compose final pixels
            for (int px = 0; px < 256; px++)
            {
                byte bgPal = bgPixels[px];
                byte sprPal = sprPixels[px];
                byte finalColor;

                bool bgOpaque = (bgPal & 0x03) != 0;
                bool sprOpaque = (sprPal & 0x03) != 0;

                // Sprite 0 hit detection
                if (sprIsSprite0[px] && bgOpaque && sprOpaque && px < 255 && showBg && showSprites)
                {
                    if (px >= 8 || (showBgLeft && showSpritesLeft))
                    {
                        _status |= 0x40; // Set sprite 0 hit
                    }
                }

                // Priority composition
                if (!bgOpaque && !sprOpaque)
                {
                    finalColor = PpuRead(0x3F00); // Universal background
                }
                else if (bgOpaque && !sprOpaque)
                {
                    finalColor = PpuRead((ushort)(0x3F00 + bgPal));
                }
                else if (!bgOpaque && sprOpaque)
                {
                    finalColor = PpuRead((ushort)(0x3F10 + sprPal));
                }
                else
                {
                    // Both opaque - priority determines which is shown
                    if (sprPriority[px] == 0)
                        finalColor = PpuRead((ushort)(0x3F10 + sprPal)); // Sprite in front
                    else
                        finalColor = PpuRead((ushort)(0x3F00 + bgPal)); // Sprite behind
                }

                int colorIndex = finalColor & 0x3F;
                if ((_mask & 0x01) != 0) // Greyscale
                    colorIndex &= 0x30;

                int fbIndex = (scanline * 256 + px) * 4;
                FrameBuffer[fbIndex + 0] = SystemPalette[colorIndex, 0]; // R
                FrameBuffer[fbIndex + 1] = SystemPalette[colorIndex, 1]; // G
                FrameBuffer[fbIndex + 2] = SystemPalette[colorIndex, 2]; // B
                FrameBuffer[fbIndex + 3] = 255;                          // A
            }

            // Update scroll registers at end of scanline if rendering enabled
            if (IsRenderingEnabled())
            {
                IncrementScrollY();
                // Copy horizontal bits from t to v
                _v = (ushort)((_v & 0x7BE0) | (_t & 0x041F));
            }
        }

        private void RenderBackgroundScanline(int scanline, byte[] bgPixels, bool showLeft)
        {
            int fineY = (_v >> 12) & 0x07;
            ushort patternBase = (ushort)((_ctrl & 0x10) != 0 ? 0x1000 : 0x0000);

            // We need to render 256 pixels, accounting for fine X scroll
            // Work through the tiles
            ushort currentV = _v;

            for (int px = 0; px < 256; px++)
            {
                if (!showLeft && px < 8)
                {
                    bgPixels[px] = 0;
                    continue;
                }

                int fineX = (px + _x) & 0x07;
                int coarseX = ((px + _x) >> 3);

                // Calculate effective v for this pixel
                int effectiveCoarseX = (_v & 0x001F) + (px + _x) / 8;
                int nametableX = (_v >> 10) & 0x01;

                // Handle nametable crossing
                if (effectiveCoarseX >= 32)
                {
                    effectiveCoarseX -= 32;
                    nametableX ^= 1;
                }

                ushort ntAddr = (ushort)(0x2000 | (nametableX << 10) | ((_v >> 5 & 0x1F) << 5) | ((_v >> 11 & 0x01) << 11) | effectiveCoarseX);
                // Simplify: use v's vertical bits
                int coarseY = (_v >> 5) & 0x1F;
                int nametableY = (_v >> 11) & 0x01;

                ntAddr = (ushort)(0x2000 | (nametableY << 11) | (nametableX << 10) | (coarseY << 5) | effectiveCoarseX);

                byte tileIndex = PpuRead(ntAddr);

                // Attribute byte
                int attrX = effectiveCoarseX / 4;
                int attrY = coarseY / 4;
                ushort attrAddr = (ushort)(0x23C0 | (nametableY << 11) | (nametableX << 10) | (attrY << 3) | attrX);
                byte attrByte = PpuRead(attrAddr);

                // Determine which 2-bit palette to use from the attribute byte
                int palShift = 0;
                if ((effectiveCoarseX & 0x02) != 0) palShift += 2;
                if ((coarseY & 0x02) != 0) palShift += 4;
                int palNum = (attrByte >> palShift) & 0x03;

                // Get pattern data
                int pixelBit = 7 - ((px + _x) & 0x07);
                ushort patternAddr = (ushort)(patternBase + tileIndex * 16 + fineY);
                byte patLow = PpuRead(patternAddr);
                byte patHigh = PpuRead((ushort)(patternAddr + 8));

                int colorBit0 = (patLow >> pixelBit) & 1;
                int colorBit1 = (patHigh >> pixelBit) & 1;
                int colorNum = colorBit0 | (colorBit1 << 1);

                if (colorNum == 0)
                    bgPixels[px] = 0;
                else
                    bgPixels[px] = (byte)(palNum * 4 + colorNum);
            }
        }

        private void RenderSpriteScanline(int scanline, byte[] sprPixels, byte[] sprPriority, bool[] sprIsSprite0, bool showLeft)
        {
            bool tallSprites = (_ctrl & 0x20) != 0;
            int spriteHeight = tallSprites ? 16 : 8;
            ushort sprPatternBase = (ushort)((_ctrl & 0x08) != 0 ? 0x1000 : 0x0000);
            int spritesFound = 0;

            // Evaluate all 64 sprites (reverse order so lower index sprites have priority)
            for (int i = 63; i >= 0; i--)
            {
                int sprY = Oam[i * 4 + 0];
                int sprTile = Oam[i * 4 + 1];
                int sprAttr = Oam[i * 4 + 2];
                int sprX = Oam[i * 4 + 3];

                int row = scanline - sprY;
                if (row < 0 || row >= spriteHeight)
                    continue;

                spritesFound++;
                if (spritesFound > 8)
                {
                    _status |= 0x20; // Sprite overflow
                    break;
                }

                bool flipH = (sprAttr & 0x40) != 0;
                bool flipV = (sprAttr & 0x80) != 0;
                int priority = (sprAttr & 0x20) != 0 ? 1 : 0; // 0=front, 1=behind bg
                int palNum = sprAttr & 0x03;

                int tileRow = row;
                if (flipV)
                    tileRow = spriteHeight - 1 - tileRow;

                ushort patternAddr;
                if (tallSprites)
                {
                    // 8x16 sprites: bit 0 of tile number selects pattern table
                    ushort baseAddr = (ushort)((sprTile & 0x01) != 0 ? 0x1000 : 0x0000);
                    int tileNum = sprTile & 0xFE;
                    if (tileRow >= 8)
                    {
                        tileNum++;
                        tileRow -= 8;
                    }
                    patternAddr = (ushort)(baseAddr + tileNum * 16 + tileRow);
                }
                else
                {
                    patternAddr = (ushort)(sprPatternBase + sprTile * 16 + tileRow);
                }

                byte patLow = PpuRead(patternAddr);
                byte patHigh = PpuRead((ushort)(patternAddr + 8));

                for (int col = 0; col < 8; col++)
                {
                    int pixX = sprX + col;
                    if (pixX >= 256)
                        continue;
                    if (!showLeft && pixX < 8)
                        continue;

                    int bit = flipH ? col : (7 - col);
                    int colorBit0 = (patLow >> bit) & 1;
                    int colorBit1 = (patHigh >> bit) & 1;
                    int colorNum = colorBit0 | (colorBit1 << 1);

                    if (colorNum == 0)
                        continue; // Transparent

                    // Lower index sprites have priority (we iterate in reverse, so overwrite)
                    sprPixels[pixX] = (byte)(palNum * 4 + colorNum);
                    sprPriority[pixX] = (byte)priority;
                    sprIsSprite0[pixX] = (i == 0);
                }
            }
        }

        private void IncrementScrollY()
        {
            if ((_v & 0x7000) != 0x7000)
            {
                _v += 0x1000; // Increment fine Y
            }
            else
            {
                _v &= 0x0FFF; // Reset fine Y
                int coarseY = (_v >> 5) & 0x1F;
                if (coarseY == 29)
                {
                    coarseY = 0;
                    _v ^= 0x0800; // Switch vertical nametable
                }
                else if (coarseY == 31)
                {
                    coarseY = 0; // Don't switch nametable
                }
                else
                {
                    coarseY++;
                }
                _v = (ushort)((_v & ~0x03E0) | (coarseY << 5));
            }
        }

        /// <summary>
        /// Performs OAM DMA transfer.
        /// </summary>
        public void OamDmaWrite(byte[] data, int offset)
        {
            for (int i = 0; i < 256; i++)
            {
                Oam[(_oamAddr + i) & 0xFF] = data[offset + i];
            }
        }
    }
}
