namespace SevenNes.Core
{
    public class Mapper3 : IMapper
    {
        private readonly Cartridge _cartridge;
        private byte _bankSelect;

        public Mapper3(Cartridge cartridge)
        {
            _cartridge = cartridge;
            _bankSelect = 0;
        }

        public byte CpuRead(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                return _cartridge.PrgRam[address & 0x1FFF];
            }

            if (address >= 0x8000 && address <= 0xBFFF)
            {
                return _cartridge.PrgRom[address & 0x3FFF];
            }

            if (address >= 0xC000 && address <= 0xFFFF)
            {
                // Mirror first bank if only one PRG bank, otherwise use last bank
                int offset;
                if (_cartridge.PrgBanks == 1)
                    offset = address & 0x3FFF;
                else
                    offset = (_cartridge.PrgBanks - 1) * 0x4000 + (address & 0x3FFF);
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
                _bankSelect = (byte)(value & 0x03);
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address <= 0x1FFF)
            {
                int offset = _bankSelect * 0x2000 + (address & 0x1FFF);
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
                    int offset = _bankSelect * 0x2000 + (address & 0x1FFF);
                    _cartridge.ChrRam[offset & 0x1FFF] = value;
                }
            }
        }
    }
}
