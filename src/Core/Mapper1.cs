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

        // PRG RAM write protect (bit 4 of PRG bank register)
        private bool _prgRamEnabled = true;

        // Consecutive-cycle write protection: ignore the first write of RMW instructions
        // that perform write-back on consecutive cycles. Track by CPU cycle count.
        private long _lastWriteCycle = -10;

        private int _prgRomLength;
        private int _chrRomLength;

        // Pre-computed bank offsets for faster reads
        private int _prgBankOffset0;
        private int _prgBankOffset1;
        private int _chrBankOffset0;
        private int _chrBankOffset1;

        public Mapper1(Cartridge cartridge)
        {
            _cartridge = cartridge;
            _prgRomLength = cartridge.PrgRom.Length;
            _chrRomLength = (cartridge.ChrRom != null) ? cartridge.ChrRom.Length : 0;
            _control = 0x0C; // default: fix last bank at $C000, 8KB CHR mode
            UpdateBankOffsets();
        }

        private void UpdateBankOffsets()
        {
            // PRG bank offsets
            int prgMode = (_control >> 2) & 0x03;
            int bankIndex = _prgBank & 0x0F;

            switch (prgMode)
            {
                case 0:
                case 1:
                    // 32KB mode: ignore low bit
                    int base32 = (bankIndex >> 1) * 0x8000;
                    _prgBankOffset0 = base32;
                    _prgBankOffset1 = base32 + 0x4000;
                    break;
                case 2:
                    // Fix first bank at $8000, switch $C000
                    _prgBankOffset0 = 0;
                    _prgBankOffset1 = bankIndex * 0x4000;
                    break;
                case 3:
                default:
                    // Switch $8000, fix last bank at $C000
                    _prgBankOffset0 = bankIndex * 0x4000;
                    _prgBankOffset1 = (_cartridge.PrgBanks - 1) * 0x4000;
                    break;
            }

            // Wrap offsets safely
            if (_prgRomLength > 0)
            {
                _prgBankOffset0 %= _prgRomLength;
                _prgBankOffset1 %= _prgRomLength;
            }

            // CHR bank offsets
            bool chrMode = (_control & 0x10) != 0;
            if (!chrMode)
            {
                // 8KB mode: use chrBank0 with bit 0 cleared
                int chrBase = (_chrBank0 >> 1) * 0x2000;
                _chrBankOffset0 = chrBase;
                _chrBankOffset1 = chrBase + 0x1000;
            }
            else
            {
                // Two 4KB banks
                _chrBankOffset0 = _chrBank0 * 0x1000;
                _chrBankOffset1 = _chrBank1 * 0x1000;
            }

            if (_chrRomLength > 0)
            {
                _chrBankOffset0 %= _chrRomLength;
                _chrBankOffset1 %= _chrRomLength;
            }
        }

        public byte CpuRead(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                return _cartridge.PrgRam[address & 0x1FFF];
            }

            if (address >= 0x8000)
            {
                int offset;
                if (address < 0xC000)
                    offset = _prgBankOffset0 + (address & 0x3FFF);
                else
                    offset = _prgBankOffset1 + (address & 0x3FFF);

                if (offset < _prgRomLength)
                    return _cartridge.PrgRom[offset];
            }

            return 0;
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                if (_prgRamEnabled)
                    _cartridge.PrgRam[address & 0x1FFF] = value;
                return;
            }

            if (address >= 0x8000)
            {
                // Consecutive-cycle write protection: RMW instructions like INC/DEC/ASL/LSR/ROL/ROR
                // first write back the original value, then write the modified value.
                // Real MMC1 ignores the first write. We detect this by checking if this write
                // is on the same or immediately consecutive CPU cycle as the last one.
                long currentCycle = GetCpuCycles();
                if (currentCycle > 0 && currentCycle - _lastWriteCycle <= 1)
                {
                    _lastWriteCycle = currentCycle;
                    return;
                }
                _lastWriteCycle = currentCycle;

                if ((value & 0x80) != 0)
                {
                    // Reset shift register
                    _shiftRegister = 0x10;
                    _writeCount = 0;
                    _control |= 0x0C;
                    UpdateBankOffsets();
                    return;
                }

                // Shift in bit 0
                _shiftRegister >>= 1;
                _shiftRegister |= (byte)((value & 1) << 4);
                _writeCount++;

                if (_writeCount == 5)
                {
                    // Address is incompletely decoded — only bits 13-14 matter
                    int reg = (address >> 13) & 0x03;
                    switch (reg)
                    {
                        case 0:
                            _control = _shiftRegister;
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
                            _prgRamEnabled = (_prgBank & 0x10) == 0;
                            break;
                    }

                    _shiftRegister = 0x10;
                    _writeCount = 0;
                    UpdateBankOffsets();
                }
            }
        }

        private long GetCpuCycles()
        {
            // Access CPU cycle counter for consecutive-write detection
            return _cartridge.CpuCycleCount;
        }

        public byte PpuRead(ushort address)
        {
            if (address <= 0x1FFF)
            {
                int offset;
                if (address < 0x1000)
                    offset = _chrBankOffset0 + (address & 0x0FFF);
                else
                    offset = _chrBankOffset1 + (address & 0x0FFF);

                if (_chrRomLength > 0)
                {
                    if (offset < _chrRomLength)
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
                if (_chrRomLength == 0)
                {
                    int offset;
                    if (address < 0x1000)
                        offset = _chrBankOffset0 + (address & 0x0FFF);
                    else
                        offset = _chrBankOffset1 + (address & 0x0FFF);

                    _cartridge.ChrRam[offset & 0x1FFF] = value;
                }
            }
        }

        public void NotifyScanline() { }

        public void NotifyCpuCycle() { }
    }
}
