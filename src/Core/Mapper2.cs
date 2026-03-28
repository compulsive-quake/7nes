namespace SevenNes.Core
{
    public class Mapper2 : IMapper
    {
        private readonly Cartridge _cartridge;
        private byte _bankSelect;

        public Mapper2(Cartridge cartridge)
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
                // Switchable bank with modulo wrapping for any ROM size
                int offset = (_bankSelect % _cartridge.PrgBanks) * 0x4000 + (address & 0x3FFF);
                return _cartridge.PrgRom[offset];
            }

            if (address >= 0xC000)
            {
                // Last bank fixed
                int offset = (_cartridge.PrgBanks - 1) * 0x4000 + (address & 0x3FFF);
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
                // Use full byte for bank selection (supports ROMs > 256KB)
                _bankSelect = value;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address <= 0x1FFF)
            {
                if (_cartridge.ChrRom != null && _cartridge.ChrRom.Length > 0)
                    return _cartridge.ChrRom[address & 0x1FFF];
                else
                    return _cartridge.ChrRam[address & 0x1FFF];
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

        public void NotifyScanline() { }
        public void NotifyCpuCycle() { }
    }
}
