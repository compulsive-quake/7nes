namespace SevenNes.Core
{
    /// <summary>
    /// Mapper 69 (Sunsoft FME-7/5A/5B). Full PRG/CHR banking with IRQ.
    /// Used by Batman: Return of the Joker, Gimmick!, Hebereke.
    /// </summary>
    public class Mapper69 : IMapper
    {
        private readonly Cartridge _cartridge;
        private int _command;
        private int[] _chrBanks = new int[8];
        private int[] _prgBanks = new int[4];
        private bool _prgRamEnabled;
        private bool _prgRamSelected;

        // IRQ
        private ushort _irqCounter;
        private bool _irqEnabled;
        private bool _irqCounterEnabled;

        private int _prgBankCount; // 8KB units

        public Mapper69(Cartridge cartridge)
        {
            _cartridge = cartridge;
            _prgBankCount = cartridge.PrgRom.Length / 0x2000;
            // Default: last bank at $E000
            _prgBanks[3] = _prgBankCount - 1;
        }

        public byte CpuRead(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                if (_prgRamSelected)
                    return _cartridge.PrgRam[address & 0x1FFF];

                int offset = (_prgBanks[0] & (_prgBankCount - 1)) * 0x2000 + (address & 0x1FFF);
                if (offset < _cartridge.PrgRom.Length)
                    return _cartridge.PrgRom[offset];
                return 0;
            }

            if (address >= 0x8000)
            {
                int slot = (address - 0x8000) / 0x2000; // 0-3
                int bank = _prgBanks[slot] & (_prgBankCount - 1);
                int offset = bank * 0x2000 + (address & 0x1FFF);
                if (offset < _cartridge.PrgRom.Length)
                    return _cartridge.PrgRom[offset];
            }

            return 0;
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                if (_prgRamSelected && _prgRamEnabled)
                    _cartridge.PrgRam[address & 0x1FFF] = value;
                return;
            }

            if (address >= 0x8000 && address <= 0x9FFF)
            {
                _command = value & 0x0F;
            }
            else if (address >= 0xA000 && address <= 0xBFFF)
            {
                switch (_command)
                {
                    case 0: case 1: case 2: case 3:
                    case 4: case 5: case 6: case 7:
                        _chrBanks[_command] = value;
                        break;
                    case 8:
                        _prgRamEnabled = (value & 0x80) != 0;
                        _prgRamSelected = (value & 0x40) != 0;
                        _prgBanks[0] = value & 0x3F;
                        break;
                    case 9:
                        _prgBanks[1] = value & 0x3F;
                        break;
                    case 10:
                        _prgBanks[2] = value & 0x3F;
                        break;
                    case 11:
                        _prgBanks[3] = value & 0x3F;
                        break;
                    case 12:
                        switch (value & 0x03)
                        {
                            case 0: _cartridge.MirrorMode = 1; break; // Vertical
                            case 1: _cartridge.MirrorMode = 0; break; // Horizontal
                            case 2: _cartridge.MirrorMode = 2; break; // Single lower
                            case 3: _cartridge.MirrorMode = 3; break; // Single upper
                        }
                        break;
                    case 13:
                        _irqEnabled = (value & 0x01) != 0;
                        _irqCounterEnabled = (value & 0x80) != 0;
                        _cartridge.IrqPending = false;
                        break;
                    case 14:
                        _irqCounter = (ushort)((_irqCounter & 0xFF00) | value);
                        break;
                    case 15:
                        _irqCounter = (ushort)((_irqCounter & 0x00FF) | (value << 8));
                        break;
                }
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address <= 0x1FFF)
            {
                int slot = address / 0x0400; // 0-7
                int chrBankCount = (_cartridge.ChrRom != null && _cartridge.ChrRom.Length > 0)
                    ? _cartridge.ChrRom.Length / 0x0400 : 0;

                if (chrBankCount > 0)
                {
                    int bank = _chrBanks[slot] & (chrBankCount - 1);
                    int offset = bank * 0x0400 + (address & 0x03FF);
                    if (offset < _cartridge.ChrRom.Length)
                        return _cartridge.ChrRom[offset];
                    return 0;
                }
                return _cartridge.ChrRam[address & 0x1FFF];
            }
            return 0;
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address <= 0x1FFF)
            {
                if (_cartridge.ChrRom == null || _cartridge.ChrRom.Length == 0)
                    _cartridge.ChrRam[address & 0x1FFF] = value;
            }
        }

        public void NotifyScanline()
        {
            if (!_irqCounterEnabled) return;

            if (_irqCounter == 0)
            {
                if (_irqEnabled)
                    _cartridge.IrqPending = true;
            }
            else
            {
                _irqCounter--;
            }
        }
    }
}
