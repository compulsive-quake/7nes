namespace SevenNes.Core
{
    /// <summary>
    /// Mapper 7 (AxROM). 32KB PRG switching, single-screen mirroring.
    /// Used by Battletoads, Marble Madness, Wizards &amp; Warriors.
    /// </summary>
    public class Mapper7 : IMapper
    {
        private readonly Cartridge _cartridge;
        private int _prgBank;

        public Mapper7(Cartridge cartridge)
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
                _prgBank = value & 0x07;
                // Bit 4: single-screen mirroring select
                _cartridge.MirrorMode = (value & 0x10) != 0 ? 3 : 2; // 3=upper, 2=lower
            }
        }

        public byte PpuRead(ushort address)
        {
            if (address <= 0x1FFF)
                return _cartridge.ChrRam[address & 0x1FFF];
            return 0;
        }

        public void PpuWrite(ushort address, byte value)
        {
            if (address <= 0x1FFF)
                _cartridge.ChrRam[address & 0x1FFF] = value;
        }

        public void NotifyScanline() { }
    }
}
