namespace SevenNes.Core
{
    public class Mapper1 : IMapper
    {
        private readonly Cartridge _cartridge;

        private byte _shiftRegister = 0x10;
        private byte _control;
        private byte _chrBank0;
        private byte _chrBank1;
        private byte _prgBank;
        private int _writeCount;

        public Mapper1(Cartridge cartridge)
        {
            _cartridge = cartridge;
            _control = 0x0C; // default: fix last bank at $C000, 8KB CHR mode
        }

        public byte CpuRead(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                return _cartridge.PrgRam[address & 0x1FFF];
            }

            if (address >= 0x8000)
            {
                int prgMode = (_control >> 2) & 0x03;
                int bankIndex = _prgBank & 0x0F;
                int offset;

                switch (prgMode)
                {
                    case 0:
                    case 1:
                        // 32KB mode: ignore low bit of bank number
                        offset = (bankIndex & 0xFE) * 0x4000 + (address & 0x7FFF);
                        break;
                    case 2:
                        // Fix first bank at $8000, switch $C000
                        if (address < 0xC000)
                            offset = address & 0x3FFF;
                        else
                            offset = bankIndex * 0x4000 + (address & 0x3FFF);
                        break;
                    case 3:
                    default:
                        // Switch $8000, fix last bank at $C000
                        if (address < 0xC000)
                            offset = bankIndex * 0x4000 + (address & 0x3FFF);
                        else
                            offset = (_cartridge.PrgBanks - 1) * 0x4000 + (address & 0x3FFF);
                        break;
                }

                if (offset < _cartridge.PrgRom.Length)
                    return _cartridge.PrgRom[offset];
            }

            return 0;
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                _cartridge.PrgRam[address & 0x1FFF] = value;
                return;
            }

            if (address >= 0x8000)
            {
                if ((value & 0x80) != 0)
                {
                    // Reset shift register
                    _shiftRegister = 0x10;
                    _writeCount = 0;
                    _control |= 0x0C;
                    return;
                }

                // Shift in bit 0
                _shiftRegister >>= 1;
                _shiftRegister |= (byte)((value & 1) << 4);
                _writeCount++;

                if (_writeCount == 5)
                {
                    int reg = (address >> 13) & 0x03;
                    switch (reg)
                    {
                        case 0:
                            _control = _shiftRegister;
                            // Update mirror mode from control bits 0-1
                            switch (_control & 0x03)
                            {
                                case 0: _cartridge.MirrorMode = 2; break; // single screen lower
                                case 1: _cartridge.MirrorMode = 3; break; // single screen upper
                                case 2: _cartridge.MirrorMode = 1; break; // vertical
                                case 3: _cartridge.MirrorMode = 0; break; // horizontal
                            }
                            break;
                        case 1:
                            _chrBank0 = _shiftRegister;
                            break;
                        case 2:
                            _chrBank1 = _shiftRegister;
                            break;
                        case 3:
                            _prgBank = _shiftRegister;
                            break;
                    }

                    _shiftRegister = 0x10;
                    _writeCount = 0;
                }
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address <= 0x1FFF)
            {
                bool chrMode = (_control & 0x10) != 0;
                int offset;

                if (!chrMode)
                {
                    // 8KB mode
                    offset = (_chrBank0 & 0x1E) * 0x1000 + (address & 0x1FFF);
                }
                else
                {
                    // Two 4KB banks
                    if (address < 0x1000)
                        offset = _chrBank0 * 0x1000 + (address & 0x0FFF);
                    else
                        offset = _chrBank1 * 0x1000 + (address & 0x0FFF);
                }

                if (_cartridge.ChrRom != null && _cartridge.ChrRom.Length > 0)
                {
                    if (offset < _cartridge.ChrRom.Length)
                        return _cartridge.ChrRom[offset];
                    return 0;
                }
                else
                {
                    return _cartridge.ChrRam[offset & 0x1FFF];
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
                    bool chrMode = (_control & 0x10) != 0;
                    int offset;

                    if (!chrMode)
                    {
                        offset = (_chrBank0 & 0x1E) * 0x1000 + (address & 0x1FFF);
                    }
                    else
                    {
                        if (address < 0x1000)
                            offset = _chrBank0 * 0x1000 + (address & 0x0FFF);
                        else
                            offset = _chrBank1 * 0x1000 + (address & 0x0FFF);
                    }

                    _cartridge.ChrRam[offset & 0x1FFF] = value;
                }
            }
        }
    }
}
