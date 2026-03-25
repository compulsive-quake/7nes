namespace SevenNes.Core
{
    /// <summary>
    /// Mapper 34 (BNROM/NINA-001). 32KB PRG switching.
    /// Used by Deadly Towers, Impossible Mission II.
    /// </summary>
    public class Mapper34 : IMapper
    {
        private readonly Cartridge _cartridge;
        private int _prgBank;
        private int _chrBank0, _chrBank1;

        public Mapper34(Cartridge cartridge)
        {
            _cartridge = cartridge;
        }

        public byte CpuRead(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
                return _cartridge.PrgRam[address & 0x1FFF];

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
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                _cartridge.PrgRam[address & 0x1FFF] = value;

                // NINA-001 variant: register writes in PRG RAM space
                if (address == 0x7FFD)
                    _prgBank = value & 0x01;
                else if (address == 0x7FFE)
                    _chrBank0 = value & 0x0F;
                else if (address == 0x7FFF)
                    _chrBank1 = value & 0x0F;
                return;
            }

            if (address >= 0x8000)
            {
                // BNROM: simple PRG bank switch
                _prgBank = value & 0x03;
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address <= 0x1FFF)
            {
                if (_cartridge.ChrRom != null && _cartridge.ChrRom.Length > 0)
                {
                    int bank = address < 0x1000 ? _chrBank0 : _chrBank1;
                    int offset = bank * 0x1000 + (address & 0x0FFF);
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
    }
}
