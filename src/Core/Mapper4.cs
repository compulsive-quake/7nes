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

        // IRQ
        private byte _irqLatch;
        private byte _irqCounter;
        private bool _irqEnabled;
        private bool _irqReload;

        private int _prgBankCount;
        private int _chrBankCount;

        public Mapper4(Cartridge cartridge, bool isMapper206)
        {
            _cartridge = cartridge;
            _isMapper206 = isMapper206;
            _prgBankCount = cartridge.PrgRom.Length / 0x2000; // 8KB banks
            _chrBankCount = (cartridge.ChrRom != null && cartridge.ChrRom.Length > 0)
                ? cartridge.ChrRom.Length / 0x0400 // 1KB banks
                : 0;

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
                int bank = GetPrgBank(address);
                int offset = bank * 0x2000 + (address & 0x1FFF);
                if (offset < _cartridge.PrgRom.Length)
                    return _cartridge.PrgRom[offset];
            }

            return 0;
        }

        private int GetPrgBank(ushort address)
        {
            int lastBank = _prgBankCount - 1;
            int secondLast = _prgBankCount - 2;

            if (address >= 0x8000 && address <= 0x9FFF)
                return _prgMode ? secondLast : (_registers[6] & (_prgBankCount - 1));
            if (address >= 0xA000 && address <= 0xBFFF)
                return _registers[7] & (_prgBankCount - 1);
            if (address >= 0xC000 && address <= 0xDFFF)
                return _prgMode ? (_registers[6] & (_prgBankCount - 1)) : secondLast;
            // $E000-$FFFF
            return lastBank;
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                _cartridge.PrgRam[address & 0x1FFF] = value;
                return;
            }

            if (address < 0x8000) return;

            bool isEven = (address & 1) == 0;

            if (address >= 0x8000 && address <= 0x9FFF)
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
            else if (address >= 0xA000 && address <= 0xBFFF)
            {
                if (!_isMapper206)
                {
                    if (isEven)
                    {
                        // Mirroring
                        _cartridge.MirrorMode = (value & 1) == 0 ? 1 : 0; // 0=vertical, 1=horizontal
                    }
                    // Odd: PRG RAM protect (ignored for simplicity)
                }
            }
            else if (address >= 0xC000 && address <= 0xDFFF)
            {
                if (!_isMapper206)
                {
                    if (isEven)
                        _irqLatch = value;
                    else
                        _irqReload = true;
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
                int bank = GetChrBank(address);
                if (_cartridge.ChrRom != null && _cartridge.ChrRom.Length > 0)
                {
                    int offset = bank * 0x0400 + (address & 0x03FF);
                    if (offset < _cartridge.ChrRom.Length)
                        return _cartridge.ChrRom[offset];
                    return 0;
                }
                else
                {
                    return _cartridge.ChrRam[address & 0x1FFF];
                }
            }
            return 0;
        }

        private int GetChrBank(ushort address)
        {
            if (_chrBankCount == 0) return 0;

            int slot = address / 0x0400; // 0-7 (1KB slots)

            if (_chrInvert)
            {
                switch (slot)
                {
                    case 0: return _registers[2] & (_chrBankCount - 1);
                    case 1: return _registers[3] & (_chrBankCount - 1);
                    case 2: return _registers[4] & (_chrBankCount - 1);
                    case 3: return _registers[5] & (_chrBankCount - 1);
                    case 4: return (_registers[0] & 0xFE) & (_chrBankCount - 1);
                    case 5: return ((_registers[0] & 0xFE) + 1) & (_chrBankCount - 1);
                    case 6: return (_registers[1] & 0xFE) & (_chrBankCount - 1);
                    case 7: return ((_registers[1] & 0xFE) + 1) & (_chrBankCount - 1);
                }
            }
            else
            {
                switch (slot)
                {
                    case 0: return (_registers[0] & 0xFE) & (_chrBankCount - 1);
                    case 1: return ((_registers[0] & 0xFE) + 1) & (_chrBankCount - 1);
                    case 2: return (_registers[1] & 0xFE) & (_chrBankCount - 1);
                    case 3: return ((_registers[1] & 0xFE) + 1) & (_chrBankCount - 1);
                    case 4: return _registers[2] & (_chrBankCount - 1);
                    case 5: return _registers[3] & (_chrBankCount - 1);
                    case 6: return _registers[4] & (_chrBankCount - 1);
                    case 7: return _registers[5] & (_chrBankCount - 1);
                }
            }

            return 0;
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address <= 0x1FFF)
            {
                if (_cartridge.ChrRom == null || _cartridge.ChrRom.Length == 0)
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
    }
}
