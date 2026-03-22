using System;
using System.IO;

namespace SevenNes.Core
{
    public class Cartridge
    {
        public byte[] PrgRom;
        public byte[] ChrRom;
        public byte[] PrgRam = new byte[8192];
        public byte[] ChrRam = new byte[8192];
        public int MapperNumber;
        public int MirrorMode;
        public int PrgBanks;
        public int ChrBanks;
        public IMapper Mapper;

        public void Load(byte[] romData)
        {
            if (romData == null || romData.Length < 16)
                throw new ArgumentException("Invalid ROM data");

            // Verify iNES header: "NES\x1A"
            if (romData[0] != 0x4E || romData[1] != 0x45 || romData[2] != 0x53 || romData[3] != 0x1A)
                throw new ArgumentException("Invalid iNES header");

            PrgBanks = romData[4]; // PRG ROM size in 16KB units
            ChrBanks = romData[5]; // CHR ROM size in 8KB units

            byte flags6 = romData[6];
            byte flags7 = romData[7];

            // Mirroring
            bool fourScreen = (flags6 & 0x08) != 0;
            if (fourScreen)
                MirrorMode = 4; // four-screen
            else
                MirrorMode = (flags6 & 0x01) != 0 ? 1 : 0; // 1=vertical, 0=horizontal

            // Mapper number
            MapperNumber = ((flags6 >> 4) & 0x0F) | (flags7 & 0xF0);

            // Trainer
            bool hasTrainer = (flags6 & 0x04) != 0;
            int offset = 16;
            if (hasTrainer)
                offset += 512;

            // Read PRG ROM
            int prgSize = PrgBanks * 16384;
            PrgRom = new byte[prgSize];
            Array.Copy(romData, offset, PrgRom, 0, prgSize);
            offset += prgSize;

            // Read CHR ROM
            int chrSize = ChrBanks * 8192;
            if (chrSize > 0)
            {
                ChrRom = new byte[chrSize];
                Array.Copy(romData, offset, ChrRom, 0, chrSize);
            }
            else
            {
                ChrRom = new byte[0];
                ChrRam = new byte[8192];
            }

            // Initialize PRG RAM
            PrgRam = new byte[8192];

            // Create mapper
            switch (MapperNumber)
            {
                case 0:
                    Mapper = new Mapper0(this);
                    break;
                case 1:
                    Mapper = new Mapper1(this);
                    break;
                case 2:
                    Mapper = new Mapper2(this);
                    break;
                case 3:
                    Mapper = new Mapper3(this);
                    break;
                default:
                    throw new NotSupportedException($"Mapper {MapperNumber} is not supported");
            }
        }
    }
}
