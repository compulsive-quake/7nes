namespace SevenNes.Core
{
    /// <summary>
    /// Mapper 9 (MMC2). 8KB PRG switching with latch-based CHR switching.
    /// Used by Mike Tyson's Punch-Out!!
    /// </summary>
    public class Mapper9 : IMapper
    {
        private readonly Cartridge _cartridge;
        private int _prgBank;
        private int _chrBank0FD, _chrBank0FE;
        private int _chrBank1FD, _chrBank1FE;
        private bool _latch0; // false=$FD selected, true=$FE selected
        private bool _latch1;

        public Mapper9(Cartridge cartridge)
        {
            _cartridge = cartridge;
            _chrBank0FE = 0;
            _chrBank1FE = 0;
        }

        public byte CpuRead(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
                return _cartridge.PrgRam[address & 0x1FFF];

            if (address >= 0x8000 && address <= 0x9FFF)
            {
                int offset = _prgBank * 0x2000 + (address & 0x1FFF);
                if (offset < _cartridge.PrgRom.Length)
                    return _cartridge.PrgRom[offset];
            }

            if (address >= 0xA000)
            {
                // Last three 8KB banks are fixed
                int fixedStart = _cartridge.PrgRom.Length - 0x6000;
                int offset = fixedStart + (address - 0xA000);
                if (offset >= 0 && offset < _cartridge.PrgRom.Length)
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

            if (address >= 0xA000 && address <= 0xAFFF)
                _prgBank = value & 0x0F;
            else if (address >= 0xB000 && address <= 0xBFFF)
                _chrBank0FD = value & 0x1F;
            else if (address >= 0xC000 && address <= 0xCFFF)
                _chrBank0FE = value & 0x1F;
            else if (address >= 0xD000 && address <= 0xDFFF)
                _chrBank1FD = value & 0x1F;
            else if (address >= 0xE000 && address <= 0xEFFF)
                _chrBank1FE = value & 0x1F;
            else if (address >= 0xF000 && address <= 0xFFFF)
                _cartridge.MirrorMode = (value & 1) == 0 ? 1 : 0; // 0=vertical, 1=horizontal
        }

        public byte PpuRead(ushort address)
        {
            if (address > 0x1FFF) return 0;

            byte result;

            if (address <= 0x0FFF)
            {
                int bank = _latch0 ? _chrBank0FE : _chrBank0FD;
                int offset = bank * 0x1000 + (address & 0x0FFF);
                result = ReadChr(offset);
            }
            else
            {
                int bank = _latch1 ? _chrBank1FE : _chrBank1FD;
                int offset = bank * 0x1000 + (address & 0x0FFF);
                result = ReadChr(offset);
            }

            // Update latches AFTER the read (based on tile fetched)
            if (address >= 0x0FD8 && address <= 0x0FDF)
                _latch0 = false;
            else if (address >= 0x0FE8 && address <= 0x0FEF)
                _latch0 = true;
            else if (address >= 0x1FD8 && address <= 0x1FDF)
                _latch1 = false;
            else if (address >= 0x1FE8 && address <= 0x1FEF)
                _latch1 = true;

            return result;
        }

        private byte ReadChr(int offset)
        {
            if (_cartridge.ChrRom != null && _cartridge.ChrRom.Length > 0)
            {
                if (offset < _cartridge.ChrRom.Length)
                    return _cartridge.ChrRom[offset];
                return 0;
            }
            return _cartridge.ChrRam[offset & 0x1FFF];
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
