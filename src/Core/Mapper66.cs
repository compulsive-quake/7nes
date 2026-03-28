namespace SevenNes.Core
{
    /// <summary>
    /// Mapper 66 (GxROM). Simple 32KB PRG + 8KB CHR switching.
    /// Used by Super Mario Bros./Duck Hunt, Doraemon.
    /// </summary>
    public class Mapper66 : IMapper
    {
        private readonly Cartridge _cartridge;
        private int _prgBank;
        private int _chrBank;

        public Mapper66(Cartridge cartridge)
        {
            _cartridge = cartridge;
        }

        public byte CpuRead(ushort address)
        {
            if (address >= 0x8000)
            {
                int offset = _prgBank * 0x8000 + (address & 0x7FFF);
                if (offset < _cartridge.PrgRom.Length)
                    return _cartridge.PrgRom[offset];
            }
            return 0;
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address >= 0x8000)
            {
                _prgBank = (value >> 4) & 0x03;
                _chrBank = value & 0x03;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address <= 0x1FFF)
            {
                if (_cartridge.ChrRom != null && _cartridge.ChrRom.Length > 0)
                {
                    int offset = _chrBank * 0x2000 + (address & 0x1FFF);
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

        public void NotifyScanline() { }
        public void NotifyCpuCycle() { }
    }
}
