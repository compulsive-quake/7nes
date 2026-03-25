namespace SevenNes.Core
{
    /// <summary>
    /// Mapper 71 (Camerica/BF9093/BF9097). 16KB PRG switching.
    /// Used by Micro Machines, Fire Hawk, Bee 52, Big Nose series.
    /// </summary>
    public class Mapper71 : IMapper
    {
        private readonly Cartridge _cartridge;
        private int _prgBank;

        public Mapper71(Cartridge cartridge)
        {
            _cartridge = cartridge;
        }

        public byte CpuRead(ushort address)
        {
            if (address >= 0x8000 && address <= 0xBFFF)
            {
                int offset = _prgBank * 0x4000 + (address & 0x3FFF);
                if (offset < _cartridge.PrgRom.Length)
                    return _cartridge.PrgRom[offset];
            }

            if (address >= 0xC000)
            {
                int offset = (_cartridge.PrgBanks - 1) * 0x4000 + (address & 0x3FFF);
                if (offset < _cartridge.PrgRom.Length)
                    return _cartridge.PrgRom[offset];
            }

            return 0;
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address >= 0x9000 && address <= 0x9FFF)
            {
                // BF9097 variant: single-screen mirroring
                _cartridge.MirrorMode = (value & 0x10) != 0 ? 3 : 2;
            }

            if (address >= 0xC000)
            {
                _prgBank = value & 0x0F;
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
