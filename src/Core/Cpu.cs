namespace SevenNes.Core
{
    public class Cpu
    {
        private Nes nes;

        // Registers
        public ushort PC;
        public byte SP;
        public byte A;
        public byte X;
        public byte Y;
        public byte Status;

        // Cycle counter
        public int Cycles;

        // Interrupt flags
        public bool NmiPending;
        public bool IrqPending;

        // Flag bit positions
        private const int FlagC = 0;
        private const int FlagZ = 1;
        private const int FlagI = 2;
        private const int FlagD = 3;
        private const int FlagB = 4;
        private const int FlagV = 6;
        private const int FlagN = 7;

        public Cpu(Nes nes)
        {
            this.nes = nes;
        }

        // --- Flag helpers ---

        private bool GetFlag(int bit)
        {
            return (Status & (1 << bit)) != 0;
        }

        private void SetFlag(int bit, bool value)
        {
            if (value)
                Status |= (byte)(1 << bit);
            else
                Status &= (byte)~(1 << bit);
        }

        private void SetZN(byte value)
        {
            SetFlag(FlagZ, value == 0);
            SetFlag(FlagN, (value & 0x80) != 0);
        }

        // --- Memory access ---

        private byte Read(ushort addr)
        {
            return nes.CpuRead(addr);
        }

        private void Write(ushort addr, byte value)
        {
            nes.CpuWrite(addr, value);
        }

        private ushort Read16(ushort addr)
        {
            byte lo = Read(addr);
            byte hi = Read((ushort)(addr + 1));
            return (ushort)(lo | (hi << 8));
        }

        private ushort Read16Bug(ushort addr)
        {
            // Indirect JMP bug: wraps within the page
            ushort lo = Read(addr);
            ushort hiAddr = (ushort)((addr & 0xFF00) | ((addr + 1) & 0x00FF));
            ushort hi = Read(hiAddr);
            return (ushort)(lo | (hi << 8));
        }

        // --- Stack operations ---

        private void Push(byte value)
        {
            Write((ushort)(0x0100 + SP), value);
            SP--;
        }

        private byte Pull()
        {
            SP++;
            return Read((ushort)(0x0100 + SP));
        }

        private void Push16(ushort value)
        {
            Push((byte)(value >> 8));
            Push((byte)(value & 0xFF));
        }

        private ushort Pull16()
        {
            ushort lo = Pull();
            ushort hi = Pull();
            return (ushort)(lo | (hi << 8));
        }

        // --- Addressing modes ---

        private ushort AddrImmediate()
        {
            ushort addr = PC;
            PC++;
            return addr;
        }

        private ushort AddrZeroPage()
        {
            ushort addr = Read(PC);
            PC++;
            return addr;
        }

        private ushort AddrZeroPageX()
        {
            ushort addr = (ushort)((Read(PC) + X) & 0xFF);
            PC++;
            return addr;
        }

        private ushort AddrZeroPageY()
        {
            ushort addr = (ushort)((Read(PC) + Y) & 0xFF);
            PC++;
            return addr;
        }

        private ushort AddrAbsolute()
        {
            ushort addr = Read16(PC);
            PC += 2;
            return addr;
        }

        private ushort AddrAbsoluteX(bool checkPageCross)
        {
            ushort baseAddr = Read16(PC);
            PC += 2;
            ushort addr = (ushort)(baseAddr + X);
            if (checkPageCross && PagesDiffer(baseAddr, addr))
                Cycles++;
            return addr;
        }

        private ushort AddrAbsoluteY(bool checkPageCross)
        {
            ushort baseAddr = Read16(PC);
            PC += 2;
            ushort addr = (ushort)(baseAddr + Y);
            if (checkPageCross && PagesDiffer(baseAddr, addr))
                Cycles++;
            return addr;
        }

        private ushort AddrIndirectX()
        {
            byte ptr = (byte)((Read(PC) + X) & 0xFF);
            PC++;
            return Read16Bug(ptr);
        }

        private ushort AddrIndirectY(bool checkPageCross)
        {
            byte ptr = Read(PC);
            PC++;
            ushort baseAddr = Read16Bug(ptr);
            ushort addr = (ushort)(baseAddr + Y);
            if (checkPageCross && PagesDiffer(baseAddr, addr))
                Cycles++;
            return addr;
        }

        private static bool PagesDiffer(ushort a, ushort b)
        {
            return (a & 0xFF00) != (b & 0xFF00);
        }

        // --- Branch helper ---

        private void Branch(bool condition)
        {
            sbyte offset = (sbyte)Read(PC);
            PC++;
            if (condition)
            {
                ushort newPC = (ushort)(PC + offset);
                Cycles++;
                if (PagesDiffer(PC, newPC))
                    Cycles++;
                PC = newPC;
            }
        }

        // --- ALU helpers ---

        private void ADC(byte value)
        {
            int carry = GetFlag(FlagC) ? 1 : 0;
            int sum = A + value + carry;
            SetFlag(FlagC, sum > 0xFF);
            SetFlag(FlagV, ((A ^ sum) & (value ^ sum) & 0x80) != 0);
            A = (byte)sum;
            SetZN(A);
        }

        private void SBC(byte value)
        {
            ADC((byte)~value);
        }

        private void Compare(byte reg, byte value)
        {
            int diff = reg - value;
            SetFlag(FlagC, reg >= value);
            SetZN((byte)diff);
        }

        private byte ASL(byte value)
        {
            SetFlag(FlagC, (value & 0x80) != 0);
            byte result = (byte)(value << 1);
            SetZN(result);
            return result;
        }

        private byte LSR(byte value)
        {
            SetFlag(FlagC, (value & 0x01) != 0);
            byte result = (byte)(value >> 1);
            SetZN(result);
            return result;
        }

        private byte ROL(byte value)
        {
            int carry = GetFlag(FlagC) ? 1 : 0;
            SetFlag(FlagC, (value & 0x80) != 0);
            byte result = (byte)((value << 1) | carry);
            SetZN(result);
            return result;
        }

        private byte ROR(byte value)
        {
            int carry = GetFlag(FlagC) ? 0x80 : 0;
            SetFlag(FlagC, (value & 0x01) != 0);
            byte result = (byte)((value >> 1) | carry);
            SetZN(result);
            return result;
        }

        // --- Reset / Interrupts ---

        public void Reset()
        {
            PC = Read16(0xFFFC);
            SP = 0xFD;
            Status = 0x24;
            Cycles = 0;
            NmiPending = false;
            IrqPending = false;
        }

        public void NMI()
        {
            Push16(PC);
            Push((byte)((Status | 0x20) & ~0x10)); // set bit 5, clear B
            PC = Read16(0xFFFA);
            SetFlag(FlagI, true);
            Cycles += 7;
            NmiPending = false;
        }

        public void IRQ()
        {
            Push16(PC);
            Push((byte)((Status | 0x20) & ~0x10));
            PC = Read16(0xFFFE);
            SetFlag(FlagI, true);
            Cycles += 7;
            IrqPending = false;
        }

        // --- Step (execute one instruction) ---

        public int Step()
        {
            // Handle interrupts
            if (NmiPending)
            {
                NMI();
                return 7;
            }

            if (IrqPending && !GetFlag(FlagI))
            {
                IRQ();
                return 7;
            }

            int startCycles = Cycles;
            byte opcode = Read(PC);
            PC++;

            switch (opcode)
            {
                // ===== 0x00 - 0x0F =====
                case 0x00: // BRK
                {
                    PC++;
                    Push16(PC);
                    Push((byte)(Status | 0x30)); // set B and bit 5
                    SetFlag(FlagI, true);
                    PC = Read16(0xFFFE);
                    Cycles += 7;
                    break;
                }
                case 0x01: // ORA (Indirect,X)
                {
                    ushort addr = AddrIndirectX();
                    A |= Read(addr);
                    SetZN(A);
                    Cycles += 6;
                    break;
                }
                case 0x05: // ORA ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    A |= Read(addr);
                    SetZN(A);
                    Cycles += 3;
                    break;
                }
                case 0x06: // ASL ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    byte val = Read(addr);
                    val = ASL(val);
                    Write(addr, val);
                    Cycles += 5;
                    break;
                }
                case 0x08: // PHP
                {
                    Push((byte)(Status | 0x30)); // set B and bit 5
                    Cycles += 3;
                    break;
                }
                case 0x09: // ORA Immediate
                {
                    ushort addr = AddrImmediate();
                    A |= Read(addr);
                    SetZN(A);
                    Cycles += 2;
                    break;
                }
                case 0x0A: // ASL Accumulator
                {
                    A = ASL(A);
                    Cycles += 2;
                    break;
                }
                case 0x0D: // ORA Absolute
                {
                    ushort addr = AddrAbsolute();
                    A |= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x0E: // ASL Absolute
                {
                    ushort addr = AddrAbsolute();
                    byte val = Read(addr);
                    val = ASL(val);
                    Write(addr, val);
                    Cycles += 6;
                    break;
                }

                // ===== 0x10 - 0x1F =====
                case 0x10: // BPL
                {
                    Branch(!GetFlag(FlagN));
                    Cycles += 2;
                    break;
                }
                case 0x11: // ORA (Indirect),Y
                {
                    ushort addr = AddrIndirectY(true);
                    A |= Read(addr);
                    SetZN(A);
                    Cycles += 5;
                    break;
                }
                case 0x15: // ORA ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    A |= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x16: // ASL ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    byte val = Read(addr);
                    val = ASL(val);
                    Write(addr, val);
                    Cycles += 6;
                    break;
                }
                case 0x18: // CLC
                {
                    SetFlag(FlagC, false);
                    Cycles += 2;
                    break;
                }
                case 0x19: // ORA Absolute,Y
                {
                    ushort addr = AddrAbsoluteY(true);
                    A |= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x1D: // ORA Absolute,X
                {
                    ushort addr = AddrAbsoluteX(true);
                    A |= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x1E: // ASL Absolute,X
                {
                    ushort addr = AddrAbsoluteX(false);
                    byte val = Read(addr);
                    val = ASL(val);
                    Write(addr, val);
                    Cycles += 7;
                    break;
                }

                // ===== 0x20 - 0x2F =====
                case 0x20: // JSR Absolute
                {
                    ushort addr = Read16(PC);
                    PC++;
                    Push16(PC);
                    PC = addr;
                    Cycles += 6;
                    break;
                }
                case 0x21: // AND (Indirect,X)
                {
                    ushort addr = AddrIndirectX();
                    A &= Read(addr);
                    SetZN(A);
                    Cycles += 6;
                    break;
                }
                case 0x24: // BIT ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    byte val = Read(addr);
                    SetFlag(FlagV, (val & 0x40) != 0);
                    SetFlag(FlagN, (val & 0x80) != 0);
                    SetFlag(FlagZ, (A & val) == 0);
                    Cycles += 3;
                    break;
                }
                case 0x25: // AND ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    A &= Read(addr);
                    SetZN(A);
                    Cycles += 3;
                    break;
                }
                case 0x26: // ROL ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    byte val = Read(addr);
                    val = ROL(val);
                    Write(addr, val);
                    Cycles += 5;
                    break;
                }
                case 0x28: // PLP
                {
                    Status = (byte)((Pull() & 0xEF) | 0x20); // clear B, set bit 5
                    Cycles += 4;
                    break;
                }
                case 0x29: // AND Immediate
                {
                    ushort addr = AddrImmediate();
                    A &= Read(addr);
                    SetZN(A);
                    Cycles += 2;
                    break;
                }
                case 0x2A: // ROL Accumulator
                {
                    A = ROL(A);
                    Cycles += 2;
                    break;
                }
                case 0x2C: // BIT Absolute
                {
                    ushort addr = AddrAbsolute();
                    byte val = Read(addr);
                    SetFlag(FlagV, (val & 0x40) != 0);
                    SetFlag(FlagN, (val & 0x80) != 0);
                    SetFlag(FlagZ, (A & val) == 0);
                    Cycles += 4;
                    break;
                }
                case 0x2D: // AND Absolute
                {
                    ushort addr = AddrAbsolute();
                    A &= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x2E: // ROL Absolute
                {
                    ushort addr = AddrAbsolute();
                    byte val = Read(addr);
                    val = ROL(val);
                    Write(addr, val);
                    Cycles += 6;
                    break;
                }

                // ===== 0x30 - 0x3F =====
                case 0x30: // BMI
                {
                    Branch(GetFlag(FlagN));
                    Cycles += 2;
                    break;
                }
                case 0x31: // AND (Indirect),Y
                {
                    ushort addr = AddrIndirectY(true);
                    A &= Read(addr);
                    SetZN(A);
                    Cycles += 5;
                    break;
                }
                case 0x35: // AND ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    A &= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x36: // ROL ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    byte val = Read(addr);
                    val = ROL(val);
                    Write(addr, val);
                    Cycles += 6;
                    break;
                }
                case 0x38: // SEC
                {
                    SetFlag(FlagC, true);
                    Cycles += 2;
                    break;
                }
                case 0x39: // AND Absolute,Y
                {
                    ushort addr = AddrAbsoluteY(true);
                    A &= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x3D: // AND Absolute,X
                {
                    ushort addr = AddrAbsoluteX(true);
                    A &= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x3E: // ROL Absolute,X
                {
                    ushort addr = AddrAbsoluteX(false);
                    byte val = Read(addr);
                    val = ROL(val);
                    Write(addr, val);
                    Cycles += 7;
                    break;
                }

                // ===== 0x40 - 0x4F =====
                case 0x40: // RTI
                {
                    Status = (byte)((Pull() & 0xEF) | 0x20);
                    PC = Pull16();
                    Cycles += 6;
                    break;
                }
                case 0x41: // EOR (Indirect,X)
                {
                    ushort addr = AddrIndirectX();
                    A ^= Read(addr);
                    SetZN(A);
                    Cycles += 6;
                    break;
                }
                case 0x45: // EOR ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    A ^= Read(addr);
                    SetZN(A);
                    Cycles += 3;
                    break;
                }
                case 0x46: // LSR ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    byte val = Read(addr);
                    val = LSR(val);
                    Write(addr, val);
                    Cycles += 5;
                    break;
                }
                case 0x48: // PHA
                {
                    Push(A);
                    Cycles += 3;
                    break;
                }
                case 0x49: // EOR Immediate
                {
                    ushort addr = AddrImmediate();
                    A ^= Read(addr);
                    SetZN(A);
                    Cycles += 2;
                    break;
                }
                case 0x4A: // LSR Accumulator
                {
                    A = LSR(A);
                    Cycles += 2;
                    break;
                }
                case 0x4C: // JMP Absolute
                {
                    PC = Read16(PC);
                    Cycles += 3;
                    break;
                }
                case 0x4D: // EOR Absolute
                {
                    ushort addr = AddrAbsolute();
                    A ^= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x4E: // LSR Absolute
                {
                    ushort addr = AddrAbsolute();
                    byte val = Read(addr);
                    val = LSR(val);
                    Write(addr, val);
                    Cycles += 6;
                    break;
                }

                // ===== 0x50 - 0x5F =====
                case 0x50: // BVC
                {
                    Branch(!GetFlag(FlagV));
                    Cycles += 2;
                    break;
                }
                case 0x51: // EOR (Indirect),Y
                {
                    ushort addr = AddrIndirectY(true);
                    A ^= Read(addr);
                    SetZN(A);
                    Cycles += 5;
                    break;
                }
                case 0x55: // EOR ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    A ^= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x56: // LSR ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    byte val = Read(addr);
                    val = LSR(val);
                    Write(addr, val);
                    Cycles += 6;
                    break;
                }
                case 0x58: // CLI
                {
                    SetFlag(FlagI, false);
                    Cycles += 2;
                    break;
                }
                case 0x59: // EOR Absolute,Y
                {
                    ushort addr = AddrAbsoluteY(true);
                    A ^= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x5D: // EOR Absolute,X
                {
                    ushort addr = AddrAbsoluteX(true);
                    A ^= Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x5E: // LSR Absolute,X
                {
                    ushort addr = AddrAbsoluteX(false);
                    byte val = Read(addr);
                    val = LSR(val);
                    Write(addr, val);
                    Cycles += 7;
                    break;
                }

                // ===== 0x60 - 0x6F =====
                case 0x60: // RTS
                {
                    PC = (ushort)(Pull16() + 1);
                    Cycles += 6;
                    break;
                }
                case 0x61: // ADC (Indirect,X)
                {
                    ushort addr = AddrIndirectX();
                    ADC(Read(addr));
                    Cycles += 6;
                    break;
                }
                case 0x65: // ADC ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    ADC(Read(addr));
                    Cycles += 3;
                    break;
                }
                case 0x66: // ROR ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    byte val = Read(addr);
                    val = ROR(val);
                    Write(addr, val);
                    Cycles += 5;
                    break;
                }
                case 0x68: // PLA
                {
                    A = Pull();
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0x69: // ADC Immediate
                {
                    ushort addr = AddrImmediate();
                    ADC(Read(addr));
                    Cycles += 2;
                    break;
                }
                case 0x6A: // ROR Accumulator
                {
                    A = ROR(A);
                    Cycles += 2;
                    break;
                }
                case 0x6C: // JMP Indirect
                {
                    ushort addr = Read16(PC);
                    PC = Read16Bug(addr);
                    Cycles += 5;
                    break;
                }
                case 0x6D: // ADC Absolute
                {
                    ushort addr = AddrAbsolute();
                    ADC(Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0x6E: // ROR Absolute
                {
                    ushort addr = AddrAbsolute();
                    byte val = Read(addr);
                    val = ROR(val);
                    Write(addr, val);
                    Cycles += 6;
                    break;
                }

                // ===== 0x70 - 0x7F =====
                case 0x70: // BVS
                {
                    Branch(GetFlag(FlagV));
                    Cycles += 2;
                    break;
                }
                case 0x71: // ADC (Indirect),Y
                {
                    ushort addr = AddrIndirectY(true);
                    ADC(Read(addr));
                    Cycles += 5;
                    break;
                }
                case 0x75: // ADC ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    ADC(Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0x76: // ROR ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    byte val = Read(addr);
                    val = ROR(val);
                    Write(addr, val);
                    Cycles += 6;
                    break;
                }
                case 0x78: // SEI
                {
                    SetFlag(FlagI, true);
                    Cycles += 2;
                    break;
                }
                case 0x79: // ADC Absolute,Y
                {
                    ushort addr = AddrAbsoluteY(true);
                    ADC(Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0x7D: // ADC Absolute,X
                {
                    ushort addr = AddrAbsoluteX(true);
                    ADC(Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0x7E: // ROR Absolute,X
                {
                    ushort addr = AddrAbsoluteX(false);
                    byte val = Read(addr);
                    val = ROR(val);
                    Write(addr, val);
                    Cycles += 7;
                    break;
                }

                // ===== 0x80 - 0x8F =====
                case 0x81: // STA (Indirect,X)
                {
                    ushort addr = AddrIndirectX();
                    Write(addr, A);
                    Cycles += 6;
                    break;
                }
                case 0x84: // STY ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    Write(addr, Y);
                    Cycles += 3;
                    break;
                }
                case 0x85: // STA ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    Write(addr, A);
                    Cycles += 3;
                    break;
                }
                case 0x86: // STX ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    Write(addr, X);
                    Cycles += 3;
                    break;
                }
                case 0x88: // DEY
                {
                    Y--;
                    SetZN(Y);
                    Cycles += 2;
                    break;
                }
                case 0x8A: // TXA
                {
                    A = X;
                    SetZN(A);
                    Cycles += 2;
                    break;
                }
                case 0x8C: // STY Absolute
                {
                    ushort addr = AddrAbsolute();
                    Write(addr, Y);
                    Cycles += 4;
                    break;
                }
                case 0x8D: // STA Absolute
                {
                    ushort addr = AddrAbsolute();
                    Write(addr, A);
                    Cycles += 4;
                    break;
                }
                case 0x8E: // STX Absolute
                {
                    ushort addr = AddrAbsolute();
                    Write(addr, X);
                    Cycles += 4;
                    break;
                }

                // ===== 0x90 - 0x9F =====
                case 0x90: // BCC
                {
                    Branch(!GetFlag(FlagC));
                    Cycles += 2;
                    break;
                }
                case 0x91: // STA (Indirect),Y
                {
                    ushort addr = AddrIndirectY(false);
                    Write(addr, A);
                    Cycles += 6;
                    break;
                }
                case 0x94: // STY ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    Write(addr, Y);
                    Cycles += 4;
                    break;
                }
                case 0x95: // STA ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    Write(addr, A);
                    Cycles += 4;
                    break;
                }
                case 0x96: // STX ZeroPage,Y
                {
                    ushort addr = AddrZeroPageY();
                    Write(addr, X);
                    Cycles += 4;
                    break;
                }
                case 0x98: // TYA
                {
                    A = Y;
                    SetZN(A);
                    Cycles += 2;
                    break;
                }
                case 0x99: // STA Absolute,Y
                {
                    ushort addr = AddrAbsoluteY(false);
                    Write(addr, A);
                    Cycles += 5;
                    break;
                }
                case 0x9A: // TXS
                {
                    SP = X;
                    Cycles += 2;
                    break;
                }
                case 0x9D: // STA Absolute,X
                {
                    ushort addr = AddrAbsoluteX(false);
                    Write(addr, A);
                    Cycles += 5;
                    break;
                }

                // ===== 0xA0 - 0xAF =====
                case 0xA0: // LDY Immediate
                {
                    ushort addr = AddrImmediate();
                    Y = Read(addr);
                    SetZN(Y);
                    Cycles += 2;
                    break;
                }
                case 0xA1: // LDA (Indirect,X)
                {
                    ushort addr = AddrIndirectX();
                    A = Read(addr);
                    SetZN(A);
                    Cycles += 6;
                    break;
                }
                case 0xA2: // LDX Immediate
                {
                    ushort addr = AddrImmediate();
                    X = Read(addr);
                    SetZN(X);
                    Cycles += 2;
                    break;
                }
                case 0xA4: // LDY ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    Y = Read(addr);
                    SetZN(Y);
                    Cycles += 3;
                    break;
                }
                case 0xA5: // LDA ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    A = Read(addr);
                    SetZN(A);
                    Cycles += 3;
                    break;
                }
                case 0xA6: // LDX ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    X = Read(addr);
                    SetZN(X);
                    Cycles += 3;
                    break;
                }
                case 0xA8: // TAY
                {
                    Y = A;
                    SetZN(Y);
                    Cycles += 2;
                    break;
                }
                case 0xA9: // LDA Immediate
                {
                    ushort addr = AddrImmediate();
                    A = Read(addr);
                    SetZN(A);
                    Cycles += 2;
                    break;
                }
                case 0xAA: // TAX
                {
                    X = A;
                    SetZN(X);
                    Cycles += 2;
                    break;
                }
                case 0xAC: // LDY Absolute
                {
                    ushort addr = AddrAbsolute();
                    Y = Read(addr);
                    SetZN(Y);
                    Cycles += 4;
                    break;
                }
                case 0xAD: // LDA Absolute
                {
                    ushort addr = AddrAbsolute();
                    A = Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0xAE: // LDX Absolute
                {
                    ushort addr = AddrAbsolute();
                    X = Read(addr);
                    SetZN(X);
                    Cycles += 4;
                    break;
                }

                // ===== 0xB0 - 0xBF =====
                case 0xB0: // BCS
                {
                    Branch(GetFlag(FlagC));
                    Cycles += 2;
                    break;
                }
                case 0xB1: // LDA (Indirect),Y
                {
                    ushort addr = AddrIndirectY(true);
                    A = Read(addr);
                    SetZN(A);
                    Cycles += 5;
                    break;
                }
                case 0xB4: // LDY ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    Y = Read(addr);
                    SetZN(Y);
                    Cycles += 4;
                    break;
                }
                case 0xB5: // LDA ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    A = Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0xB6: // LDX ZeroPage,Y
                {
                    ushort addr = AddrZeroPageY();
                    X = Read(addr);
                    SetZN(X);
                    Cycles += 4;
                    break;
                }
                case 0xB8: // CLV
                {
                    SetFlag(FlagV, false);
                    Cycles += 2;
                    break;
                }
                case 0xB9: // LDA Absolute,Y
                {
                    ushort addr = AddrAbsoluteY(true);
                    A = Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0xBA: // TSX
                {
                    X = SP;
                    SetZN(X);
                    Cycles += 2;
                    break;
                }
                case 0xBC: // LDY Absolute,X
                {
                    ushort addr = AddrAbsoluteX(true);
                    Y = Read(addr);
                    SetZN(Y);
                    Cycles += 4;
                    break;
                }
                case 0xBD: // LDA Absolute,X
                {
                    ushort addr = AddrAbsoluteX(true);
                    A = Read(addr);
                    SetZN(A);
                    Cycles += 4;
                    break;
                }
                case 0xBE: // LDX Absolute,Y
                {
                    ushort addr = AddrAbsoluteY(true);
                    X = Read(addr);
                    SetZN(X);
                    Cycles += 4;
                    break;
                }

                // ===== 0xC0 - 0xCF =====
                case 0xC0: // CPY Immediate
                {
                    ushort addr = AddrImmediate();
                    Compare(Y, Read(addr));
                    Cycles += 2;
                    break;
                }
                case 0xC1: // CMP (Indirect,X)
                {
                    ushort addr = AddrIndirectX();
                    Compare(A, Read(addr));
                    Cycles += 6;
                    break;
                }
                case 0xC4: // CPY ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    Compare(Y, Read(addr));
                    Cycles += 3;
                    break;
                }
                case 0xC5: // CMP ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    Compare(A, Read(addr));
                    Cycles += 3;
                    break;
                }
                case 0xC6: // DEC ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    byte val = (byte)(Read(addr) - 1);
                    Write(addr, val);
                    SetZN(val);
                    Cycles += 5;
                    break;
                }
                case 0xC8: // INY
                {
                    Y++;
                    SetZN(Y);
                    Cycles += 2;
                    break;
                }
                case 0xC9: // CMP Immediate
                {
                    ushort addr = AddrImmediate();
                    Compare(A, Read(addr));
                    Cycles += 2;
                    break;
                }
                case 0xCA: // DEX
                {
                    X--;
                    SetZN(X);
                    Cycles += 2;
                    break;
                }
                case 0xCC: // CPY Absolute
                {
                    ushort addr = AddrAbsolute();
                    Compare(Y, Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xCD: // CMP Absolute
                {
                    ushort addr = AddrAbsolute();
                    Compare(A, Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xCE: // DEC Absolute
                {
                    ushort addr = AddrAbsolute();
                    byte val = (byte)(Read(addr) - 1);
                    Write(addr, val);
                    SetZN(val);
                    Cycles += 6;
                    break;
                }

                // ===== 0xD0 - 0xDF =====
                case 0xD0: // BNE
                {
                    Branch(!GetFlag(FlagZ));
                    Cycles += 2;
                    break;
                }
                case 0xD1: // CMP (Indirect),Y
                {
                    ushort addr = AddrIndirectY(true);
                    Compare(A, Read(addr));
                    Cycles += 5;
                    break;
                }
                case 0xD5: // CMP ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    Compare(A, Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xD6: // DEC ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    byte val = (byte)(Read(addr) - 1);
                    Write(addr, val);
                    SetZN(val);
                    Cycles += 6;
                    break;
                }
                case 0xD8: // CLD
                {
                    SetFlag(FlagD, false);
                    Cycles += 2;
                    break;
                }
                case 0xD9: // CMP Absolute,Y
                {
                    ushort addr = AddrAbsoluteY(true);
                    Compare(A, Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xDD: // CMP Absolute,X
                {
                    ushort addr = AddrAbsoluteX(true);
                    Compare(A, Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xDE: // DEC Absolute,X
                {
                    ushort addr = AddrAbsoluteX(false);
                    byte val = (byte)(Read(addr) - 1);
                    Write(addr, val);
                    SetZN(val);
                    Cycles += 7;
                    break;
                }

                // ===== 0xE0 - 0xEF =====
                case 0xE0: // CPX Immediate
                {
                    ushort addr = AddrImmediate();
                    Compare(X, Read(addr));
                    Cycles += 2;
                    break;
                }
                case 0xE1: // SBC (Indirect,X)
                {
                    ushort addr = AddrIndirectX();
                    SBC(Read(addr));
                    Cycles += 6;
                    break;
                }
                case 0xE4: // CPX ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    Compare(X, Read(addr));
                    Cycles += 3;
                    break;
                }
                case 0xE5: // SBC ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    SBC(Read(addr));
                    Cycles += 3;
                    break;
                }
                case 0xE6: // INC ZeroPage
                {
                    ushort addr = AddrZeroPage();
                    byte val = (byte)(Read(addr) + 1);
                    Write(addr, val);
                    SetZN(val);
                    Cycles += 5;
                    break;
                }
                case 0xE8: // INX
                {
                    X++;
                    SetZN(X);
                    Cycles += 2;
                    break;
                }
                case 0xE9: // SBC Immediate
                {
                    ushort addr = AddrImmediate();
                    SBC(Read(addr));
                    Cycles += 2;
                    break;
                }
                case 0xEA: // NOP
                {
                    Cycles += 2;
                    break;
                }
                case 0xEC: // CPX Absolute
                {
                    ushort addr = AddrAbsolute();
                    Compare(X, Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xED: // SBC Absolute
                {
                    ushort addr = AddrAbsolute();
                    SBC(Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xEE: // INC Absolute
                {
                    ushort addr = AddrAbsolute();
                    byte val = (byte)(Read(addr) + 1);
                    Write(addr, val);
                    SetZN(val);
                    Cycles += 6;
                    break;
                }

                // ===== 0xF0 - 0xFF =====
                case 0xF0: // BEQ
                {
                    Branch(GetFlag(FlagZ));
                    Cycles += 2;
                    break;
                }
                case 0xF1: // SBC (Indirect),Y
                {
                    ushort addr = AddrIndirectY(true);
                    SBC(Read(addr));
                    Cycles += 5;
                    break;
                }
                case 0xF5: // SBC ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    SBC(Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xF6: // INC ZeroPage,X
                {
                    ushort addr = AddrZeroPageX();
                    byte val = (byte)(Read(addr) + 1);
                    Write(addr, val);
                    SetZN(val);
                    Cycles += 6;
                    break;
                }
                case 0xF8: // SED
                {
                    SetFlag(FlagD, true);
                    Cycles += 2;
                    break;
                }
                case 0xF9: // SBC Absolute,Y
                {
                    ushort addr = AddrAbsoluteY(true);
                    SBC(Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xFD: // SBC Absolute,X
                {
                    ushort addr = AddrAbsoluteX(true);
                    SBC(Read(addr));
                    Cycles += 4;
                    break;
                }
                case 0xFE: // INC Absolute,X
                {
                    ushort addr = AddrAbsoluteX(false);
                    byte val = (byte)(Read(addr) + 1);
                    Write(addr, val);
                    SetZN(val);
                    Cycles += 7;
                    break;
                }

                // Undefined / illegal opcodes - treat as NOP
                default:
                {
                    Cycles += 2;
                    break;
                }
            }

            return Cycles - startCycles;
        }
    }
}
