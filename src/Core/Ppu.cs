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

        // Per-scanline sprite buffers (pre-evaluated at dot 0)
        private byte[] _sprPixels = new byte[256];
        private byte[] _sprPriority = new byte[256]; // 0 = in front of bg, 1 = behind bg
        private bool[] _sprIsSprite0 = new bool[256];

        // NES system palette (64 colors, RGB) — 2C02G from nesdev.org/wiki/PPU_palettes
        private static readonly byte[,] SystemPalette = new byte[64, 3]
        {
            {  98,  98,  98 }, // 0x00
            {   0,  28, 149 }, // 0x01
            {  25,   4, 172 }, // 0x02
            {  66,   0, 157 }, // 0x03
            {  97,   0, 107 }, // 0x04
            { 110,   0,  37 }, // 0x05
            { 101,   5,   0 }, // 0x06
            {  73,  30,   0 }, // 0x07
            {  34,  55,   0 }, // 0x08
            {   0,  73,   0 }, // 0x09
            {   0,  79,   0 }, // 0x0A
            {   0,  72,  22 }, // 0x0B
            {   0,  53,  94 }, // 0x0C
            {   0,   0,   0 }, // 0x0D
            {   0,   0,   0 }, // 0x0E
            {   0,   0,   0 }, // 0x0F
            { 171, 171, 171 }, // 0x10
            {  12,  78, 219 }, // 0x11
            {  61,  46, 255 }, // 0x12
            { 113,  21, 243 }, // 0x13
            { 155,  11, 185 }, // 0x14
            { 176,  18,  98 }, // 0x15
            { 169,  39,   4 }, // 0x16
            { 137,  70,   0 }, // 0x17
            {  87, 102,   0 }, // 0x18
            {  35, 127,   0 }, // 0x19
            {   0, 137,   0 }, // 0x1A
            {   0, 131,  50 }, // 0x1B
            {   0, 109, 144 }, // 0x1C
            {   0,   0,   0 }, // 0x1D
            {   0,   0,   0 }, // 0x1E
            {   0,   0,   0 }, // 0x1F
            { 255, 255, 255 }, // 0x20
            {  87, 165, 255 }, // 0x21
            { 130, 135, 255 }, // 0x22
            { 180, 109, 255 }, // 0x23
            { 223,  96, 255 }, // 0x24
            { 248,  99, 198 }, // 0x25
            { 248, 116, 109 }, // 0x26
            { 222, 144,  32 }, // 0x27
            { 179, 174,   0 }, // 0x28
            { 129, 200,   0 }, // 0x29
            {  86, 213,  34 }, // 0x2A
            {  61, 211, 111 }, // 0x2B
            {  62, 193, 200 }, // 0x2C
            {  78,  78,  78 }, // 0x2D
            {   0,   0,   0 }, // 0x2E
            {   0,   0,   0 }, // 0x2F
            { 255, 255, 255 }, // 0x30
            { 190, 224, 255 }, // 0x31
            { 205, 212, 255 }, // 0x32
            { 224, 202, 255 }, // 0x33
            { 241, 196, 255 }, // 0x34
            { 252, 196, 239 }, // 0x35
            { 253, 202, 206 }, // 0x36
            { 245, 212, 175 }, // 0x37
            { 230, 223, 156 }, // 0x38
            { 211, 233, 154 }, // 0x39
            { 194, 239, 168 }, // 0x3A
            { 183, 239, 196 }, // 0x3B
            { 182, 234, 229 }, // 0x3C
            { 184, 184, 184 }, // 0x3D
            {   0,   0,   0 }, // 0x3E
            {   0,   0,   0 }, // 0x3F
        };

        // sRGB-to-linear lookup table: compensates for Unity's linear→sRGB display gamma
        private static readonly byte[] SrgbToLinear = BuildSrgbToLinearTable();
        private static byte[] BuildSrgbToLinearTable()
        {
            var table = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                double s = i / 255.0;
                double linear = s <= 0.04045 ? s / 12.92 : System.Math.Pow((s + 0.055) / 1.055, 2.4);
                table[i] = (byte)(linear * 255.0 + 0.5);
            }
            return table;
        }

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
            bool rendering = IsRenderingEnabled();
            bool visibleScanline = Scanline >= 0 && Scanline <= 239;
            bool preRenderScanline = Scanline == 261;

            if (rendering && (visibleScanline || preRenderScanline))
            {
                // --- Sprite evaluation at dot 0 (visible scanlines only) ---
                if (visibleScanline && Cycle == 0)
                {
                    EvaluateSpriteBuffers(Scanline, _sprPixels, _sprPriority, _sprIsSprite0);
                }

                // --- Pixel output at dots 1-256 (visible scanlines only) ---
                if (visibleScanline && Cycle >= 1 && Cycle <= 256)
                {
                    RenderPixel(Scanline, Cycle - 1);
                }

                // Increment fine Y at dot 256
                if (Cycle == 256)
                {
                    IncrementScrollY();
                }

                // Copy horizontal bits from t to v at dot 257
                if (Cycle == 257)
                {
                    _v = (ushort)((_v & 0x7BE0) | (_t & 0x041F));
                }

                // Pre-render: copy vertical bits from t to v at dots 280-304
                if (preRenderScanline && Cycle >= 280 && Cycle <= 304)
                {
                    _v = (ushort)((_v & 0x041F) | (_t & 0x7BE0));
                }
            }

            // --- Clear flags on pre-render scanline (always, even if rendering disabled) ---
            if (preRenderScanline && Cycle == 1)
            {
                _status &= 0x1F;
                _nmiTriggered = false;
            }

            // --- Notify mapper of scanline for IRQ counting (at cycle 260) ---
            if (Cycle == 260 && rendering && (visibleScanline || preRenderScanline))
            {
                _nes.Cartridge.Mapper.NotifyScanline();
            }

            // --- VBlank scanline (241) ---
            if (Scanline == 241 && Cycle == 1)
            {
                _status |= 0x80; // Set vblank flag
                if ((_ctrl & 0x80) != 0 && !_nmiTriggered)
                {
                    _nmiTriggered = true;
                    _nes.Cpu.NmiPending = true;
                }
            }

            // --- Advance cycle and scanline ---
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

        private void RenderPixel(int scanline, int px)
        {
            bool showBg = (_mask & 0x08) != 0;
            bool showSprites = (_mask & 0x10) != 0;
            bool showBgLeft = (_mask & 0x02) != 0;
            bool showSpritesLeft = (_mask & 0x04) != 0;

            // Get background pixel
            byte bgPal = 0;
            if (showBg && (showBgLeft || px >= 8))
            {
                bgPal = GetBackgroundPixel(px);
            }

            // Get sprite pixel (from pre-evaluated buffers)
            byte sprPal = 0;
            byte sprPri = 0;
            bool spr0 = false;
            if (showSprites && (showSpritesLeft || px >= 8))
            {
                sprPal = _sprPixels[px];
                sprPri = _sprPriority[px];
                spr0 = _sprIsSprite0[px];
            }

            bool bgOpaque = (bgPal & 0x03) != 0;
            bool sprOpaque = (sprPal & 0x03) != 0;

            // Sprite 0 hit detection
            if (spr0 && bgOpaque && sprOpaque && px < 255 && showBg && showSprites)
            {
                if (px >= 8 || (showBgLeft && showSpritesLeft))
                {
                    _status |= 0x40; // Set sprite 0 hit
                }
            }

            // Priority composition
            byte finalColor;
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
                if (sprPri == 0)
                    finalColor = PpuRead((ushort)(0x3F10 + sprPal)); // Sprite in front
                else
                    finalColor = PpuRead((ushort)(0x3F00 + bgPal)); // Sprite behind
            }

            int colorIndex = finalColor & 0x3F;
            if ((_mask & 0x01) != 0) // Greyscale
                colorIndex &= 0x30;

            int fbIndex = (scanline * 256 + px) * 4;
            FrameBuffer[fbIndex + 0] = SrgbToLinear[SystemPalette[colorIndex, 0]]; // R
            FrameBuffer[fbIndex + 1] = SrgbToLinear[SystemPalette[colorIndex, 1]]; // G
            FrameBuffer[fbIndex + 2] = SrgbToLinear[SystemPalette[colorIndex, 2]]; // B
            FrameBuffer[fbIndex + 3] = 255;                                        // A
        }

        private byte GetBackgroundPixel(int px)
        {
            int fineY = (_v >> 12) & 0x07;
            ushort patternBase = (ushort)((_ctrl & 0x10) != 0 ? 0x1000 : 0x0000);

            // Calculate effective tile position from _v's starting scroll + pixel offset
            int effectiveCoarseX = (_v & 0x001F) + (px + _x) / 8;
            int nametableX = (_v >> 10) & 0x01;

            // Handle nametable crossing
            if (effectiveCoarseX >= 32)
            {
                effectiveCoarseX -= 32;
                nametableX ^= 1;
            }

            int coarseY = (_v >> 5) & 0x1F;
            int nametableY = (_v >> 11) & 0x01;

            ushort ntAddr = (ushort)(0x2000 | (nametableY << 11) | (nametableX << 10) | (coarseY << 5) | effectiveCoarseX);
            byte tileIndex = PpuRead(ntAddr);

            // Attribute byte
            ushort attrAddr = (ushort)(0x23C0 | (nametableY << 11) | (nametableX << 10) | ((coarseY >> 2) << 3) | (effectiveCoarseX >> 2));
            byte attrByte = PpuRead(attrAddr);

            // Determine which 2-bit palette to use from the attribute byte
            int palShift = ((effectiveCoarseX & 0x02) != 0 ? 2 : 0) + ((coarseY & 0x02) != 0 ? 4 : 0);
            int palNum = (attrByte >> palShift) & 0x03;

            // Get pattern data
            int pixelBit = 7 - ((px + _x) & 0x07);
            ushort patternAddr = (ushort)(patternBase + tileIndex * 16 + fineY);
            byte patLow = PpuRead(patternAddr);
            byte patHigh = PpuRead((ushort)(patternAddr + 8));

            int colorBit0 = (patLow >> pixelBit) & 1;
            int colorBit1 = (patHigh >> pixelBit) & 1;
            int colorNum = colorBit0 | (colorBit1 << 1);

            return colorNum == 0 ? (byte)0 : (byte)(palNum * 4 + colorNum);
        }

        private void EvaluateSpriteBuffers(int scanline, byte[] pixBuf, byte[] priBuf, bool[] sp0Buf)
        {
            // Clear sprite buffers
            Array.Clear(pixBuf, 0, 256);
            Array.Clear(priBuf, 0, 256);
            Array.Clear(sp0Buf, 0, 256);

            bool tallSprites = (_ctrl & 0x20) != 0;
            int spriteHeight = tallSprites ? 16 : 8;
            ushort sprPatternBase = (ushort)((_ctrl & 0x08) != 0 ? 0x1000 : 0x0000);

            // Pass 1: find the first 8 sprites on this scanline (forward order)
            int spritesFound = 0;
            int[] spriteIndices = new int[8];
            for (int i = 0; i < 64 && spritesFound < 8; i++)
            {
                int sprY = Oam[i * 4 + 0];
                int row = scanline - sprY - 1;
                if (row >= 0 && row < spriteHeight)
                    spriteIndices[spritesFound++] = i;
            }

            // Pass 2: render in reverse order so lower-index sprites overwrite (higher priority)
            for (int s = spritesFound - 1; s >= 0; s--)
            {
                int i = spriteIndices[s];
                int sprY = Oam[i * 4 + 0];
                int sprTile = Oam[i * 4 + 1];
                int sprAttr = Oam[i * 4 + 2];
                int sprX = Oam[i * 4 + 3];

                int row = scanline - sprY - 1;

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

                    int bit = flipH ? col : (7 - col);
                    int colorBit0 = (patLow >> bit) & 1;
                    int colorBit1 = (patHigh >> bit) & 1;
                    int colorNum = colorBit0 | (colorBit1 << 1);

                    if (colorNum == 0)
                        continue; // Transparent

                    // Lower index sprites have priority (we iterate in reverse, so overwrite)
                    pixBuf[pixX] = (byte)(palNum * 4 + colorNum);
                    priBuf[pixX] = (byte)priority;
                    sp0Buf[pixX] = (i == 0);
                }
            }
        }

        private void IncrementScrollX()
        {
            if ((_v & 0x001F) == 31)
            {
                _v &= unchecked((ushort)~0x001F); // Clear coarse X
                _v ^= 0x0400;                     // Switch horizontal nametable
            }
            else
            {
                _v++;
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
