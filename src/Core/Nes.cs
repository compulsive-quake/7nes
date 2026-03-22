using System;
using System.IO;

namespace SevenNes.Core
{
    public class Nes
    {
        public Cpu Cpu { get; private set; }
        public Ppu Ppu { get; private set; }
        public Cartridge Cartridge { get; private set; }
        public Controller Controller1 { get; private set; }
        public Controller Controller2 { get; private set; }

        public byte[] Ram = new byte[2048];
        public string CurrentRomName { get; set; }

        private int _dmaCyclesRemaining;

        public Nes()
        {
            Cpu = new Cpu(this);
            Ppu = new Ppu(this);
            Cartridge = new Cartridge();
            Controller1 = new Controller();
            Controller2 = new Controller();
        }

        public void LoadRom(byte[] romData)
        {
            Cartridge.Load(romData);
            Reset();
        }

        public void LoadRom(string filePath)
        {
            byte[] romData = File.ReadAllBytes(filePath);
            LoadRom(romData);
            CurrentRomName = Path.GetFileNameWithoutExtension(filePath);
        }

        public void Reset()
        {
            Cpu.Reset();
            Ppu.Reset();
            Array.Clear(Ram, 0, Ram.Length);
            _dmaCyclesRemaining = 0;
        }

        public void RunFrame()
        {
            Ppu.FrameComplete = false;
            while (!Ppu.FrameComplete)
            {
                if (_dmaCyclesRemaining > 0)
                {
                    _dmaCyclesRemaining--;
                    // 3 PPU cycles per CPU cycle
                    Ppu.Step();
                    Ppu.Step();
                    Ppu.Step();
                }
                else
                {
                    int cpuCycles = Cpu.Step();
                    for (int i = 0; i < cpuCycles * 3; i++)
                    {
                        Ppu.Step();
                        if (Ppu.FrameComplete)
                            break;
                    }
                }
            }
        }

        public byte[] GetFrameBuffer()
        {
            return Ppu.FrameBuffer;
        }

        public byte CpuRead(ushort address)
        {
            if (address <= 0x1FFF)
            {
                // Internal RAM (mirrored)
                return Ram[address & 0x07FF];
            }
            else if (address <= 0x3FFF)
            {
                // PPU registers (mirrored every 8 bytes)
                return Ppu.ReadRegister(address);
            }
            else if (address == 0x4016)
            {
                return Controller1.Read();
            }
            else if (address == 0x4017)
            {
                return Controller2.Read();
            }
            else if (address <= 0x401F)
            {
                // APU/IO registers - return 0
                return 0;
            }
            else
            {
                // Cartridge space ($4020-$FFFF)
                return Cartridge.Mapper.CpuRead(address);
            }
        }

        public void CpuWrite(ushort address, byte value)
        {
            if (address <= 0x1FFF)
            {
                // Internal RAM (mirrored)
                Ram[address & 0x07FF] = value;
            }
            else if (address <= 0x3FFF)
            {
                // PPU registers (mirrored every 8 bytes)
                Ppu.WriteRegister(address, value);
            }
            else if (address == 0x4014)
            {
                // OAM DMA
                ushort srcAddr = (ushort)(value << 8);
                byte[] dmaData = new byte[256];
                for (int i = 0; i < 256; i++)
                {
                    dmaData[i] = CpuRead((ushort)(srcAddr + i));
                }
                Ppu.OamDmaWrite(dmaData, 0);
                _dmaCyclesRemaining = 513;
            }
            else if (address == 0x4016)
            {
                Controller1.Write(value);
                Controller2.Write(value);
            }
            else if (address <= 0x401F)
            {
                // APU/IO registers - ignore
            }
            else
            {
                // Cartridge space ($4020-$FFFF)
                Cartridge.Mapper.CpuWrite(address, value);
            }
        }
    }
}
