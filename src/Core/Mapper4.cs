namespace SevenNes.Core
{
    /// <summary>
    /// Mapper 4 (MMC3/TxROM) and Mapper 206 (simplified MMC3).
    /// Used by Super Mario Bros. 2, Super Mario Bros. 3, Mega Man 3-6, Kirby's Adventure, etc.
    /// </summary>
    public class Mapper4 : IMapper
    {
        private readonly Cartridge _cartridge;
        private readonly bool _isMapper206;

        private int _bankSelect;
        private bool _prgMode;    // false = $8000 switchable, true = $C000 switchable
        private bool _chrInvert;  // false = 2KB at $0000, true = 2KB at $1000
        private int[] _registers = new int[8];

        // IRQ — simple counter model matching proven reference implementation:
        // counter==0 → reload from latch; counter>0 → decrement, fire on transition to zero.
        private byte _irqLatch;
        private byte _irqCounter;
        private bool _irqEnabled;
        private bool _irqReload;

        // PRG RAM write protect
        private bool _prgRamEnabled = true;

        private int _prgBankCount;
        private int _chrBankCount;
        private int _prgRomLength;
        private int _chrRomLength;

        public Mapper4(Cartridge cartridge, bool isMapper206)
        {
            _cartridge = cartridge;
            _isMapper206 = isMapper206;
            _prgRomLength = cartridge.PrgRom.Length;
            _chrRomLength = (cartridge.ChrRom != null) ? cartridge.ChrRom.Length : 0;
            _prgBankCount = _prgRomLength / 0x2000; // 8KB banks
            _chrBankCount = _chrRomLength > 0 ? _chrRomLength / 0x0400 : 0; // 1KB banks

            // Default register values
            _registers[6] = 0;
            _registers[7] = 1;
        }

        public byte CpuRead(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                return _cartridge.PrgRam[address & 0x1FFF];
            }

            if (address >= 0x8000)
            {
                int offset = GetPrgOffset(address);
                if (offset < _prgRomLength)
                    return _cartridge.PrgRom[offset];
            }

            return 0;
        }

        private int GetPrgOffset(ushort address)
        {
            int slot = (address - 0x8000) >> 13; // 0-3
            int bankOffset;

            switch (slot)
            {
                case 0:
                    bankOffset = _prgMode
                        ? (_prgRomLength - 0x4000) // second-to-last 8KB bank
                        : (_registers[6] * 0x2000);
                    break;
                case 1:
                    bankOffset = _registers[7] * 0x2000;
                    break;
                case 2:
                    bankOffset = _prgMode
                        ? (_registers[6] * 0x2000)
                        : (_prgRomLength - 0x4000); // second-to-last 8KB bank
                    break;
                default: // case 3
                    bankOffset = _prgRomLength - 0x2000; // last 8KB bank
                    break;
            }

            // Use modulo for safe wrapping (handles non-power-of-2 ROM sizes)
            return (bankOffset % _prgRomLength) + (address & 0x1FFF);
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                if (_prgRamEnabled)
                    _cartridge.PrgRam[address & 0x1FFF] = value;
                return;
            }

            if (address < 0x8000) return;

            bool isEven = (address & 1) == 0;

            if (address < 0xA000) // $8000-$9FFF
            {
                if (isEven)
                {
                    // Bank select
                    _bankSelect = value & 0x07;
                    if (!_isMapper206)
                    {
                        _prgMode = (value & 0x40) != 0;
                        _chrInvert = (value & 0x80) != 0;
                    }
                }
                else
                {
                    // Bank data
                    _registers[_bankSelect] = value;
                }
            }
            else if (address < 0xC000) // $A000-$BFFF
            {
                if (!_isMapper206)
                {
                    if (isEven)
                    {
                        // Mirroring
                        _cartridge.MirrorMode = (value & 1) == 0 ? 1 : 0; // 0=vertical, 1=horizontal
                    }
                    else
                    {
                        // PRG RAM protect: bit 7 = enabled, bit 6 = write protect
                        _prgRamEnabled = (value & 0xC0) == 0x80;
                    }
                }
            }
            else if (address < 0xE000) // $C000-$DFFF
            {
                if (!_isMapper206)
                {
                    if (isEven)
                    {
                        _irqLatch = value;
                    }
                    else
                    {
                        // Reload: clear counter so it reloads from latch on next scanline clock
                        _irqReload = true;
                        _irqCounter = 0;
                    }
                }
            }
            else // $E000-$FFFF
            {
                if (!_isMapper206)
                {
                    if (isEven)
                    {
                        _irqEnabled = false;
                        _cartridge.IrqPending = false;
                    }
                    else
                    {
                        _irqEnabled = true;
                    }
                }
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address <= 0x1FFF)
            {
                if (_chrRomLength > 0)
                {
                    int offset = GetChrOffset(address);
                    return _cartridge.ChrRom[offset];
                }
                else
                {
                    return _cartridge.ChrRam[address & 0x1FFF];
                }
            }
            return 0;
        }

        private int GetChrOffset(ushort address)
        {
            int slot = address >> 10; // 0-7 (1KB slots)
            int bankNum;

            if (_chrInvert)
            {
                switch (slot)
                {
                    case 0: bankNum = _registers[2]; break;
                    case 1: bankNum = _registers[3]; break;
                    case 2: bankNum = _registers[4]; break;
                    case 3: bankNum = _registers[5]; break;
                    case 4: bankNum = _registers[0] & 0xFE; break;
                    case 5: bankNum = (_registers[0] & 0xFE) | 0x01; break;
                    case 6: bankNum = _registers[1] & 0xFE; break;
                    default: bankNum = (_registers[1] & 0xFE) | 0x01; break;
                }
            }
            else
            {
                switch (slot)
                {
                    case 0: bankNum = _registers[0] & 0xFE; break;
                    case 1: bankNum = (_registers[0] & 0xFE) | 0x01; break;
                    case 2: bankNum = _registers[1] & 0xFE; break;
                    case 3: bankNum = (_registers[1] & 0xFE) | 0x01; break;
                    case 4: bankNum = _registers[2]; break;
                    case 5: bankNum = _registers[3]; break;
                    case 6: bankNum = _registers[4]; break;
                    default: bankNum = _registers[5]; break;
                }
            }

            // Use modulo for safe wrapping (handles any ROM size)
            return (bankNum * 0x0400 % _chrRomLength) + (address & 0x03FF);
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address <= 0x1FFF)
            {
                if (_chrRomLength == 0)
                {
                    _cartridge.ChrRam[address & 0x1FFF] = value;
                }
            }
        }

        public void NotifyScanline()
        {
            if (_isMapper206) return;

            if (_irqCounter == 0 || _irqReload)
            {
                _irqCounter = _irqLatch;
                _irqReload = false;
            }
            else
            {
                _irqCounter--;
            }

            if (_irqCounter == 0 && _irqEnabled)
            {
                _cartridge.IrqPending = true;
            }
        }

        public void NotifyCpuCycle() { }
    }
}
