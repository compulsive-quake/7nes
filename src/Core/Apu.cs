using System;

namespace SevenNes.Core
{
    public class Apu
    {
        // Constants
        private const int CPU_CLOCK = 1789773;
        private const int SAMPLE_BUFFER_SIZE = 8192;

        // Sample buffer (ring buffer) - written by emulator thread, read by audio thread
        private readonly float[] _sampleBuffer = new float[SAMPLE_BUFFER_SIZE];
        private volatile int _sampleWritePos;
        private volatile int _sampleReadPos;

        // Cycle tracking for sample generation
        private double _sampleCycleCounter;
        private double _cyclesPerSample;
        private int _sampleRate;

        // Frame counter
        private int _frameCounterCycle;
        private bool _frameCounterMode; // false = 4-step, true = 5-step
        private bool _frameIrqInhibit;
        private bool _frameIrqFlag;

        // Pulse 1
        private int _pulse1Duty;
        private int _pulse1DutyPos;
        private bool _pulse1LengthHalt;
        private bool _pulse1ConstantVolume;
        private int _pulse1Volume;
        private int _pulse1SweepEnabled;
        private int _pulse1SweepPeriod;
        private bool _pulse1SweepNegate;
        private int _pulse1SweepShift;
        private int _pulse1SweepDivider;
        private bool _pulse1SweepReload;
        private int _pulse1TimerPeriod;
        private int _pulse1TimerValue;
        private int _pulse1LengthCounter;
        private int _pulse1EnvelopeDivider;
        private int _pulse1EnvelopeDecay;
        private bool _pulse1EnvelopeStart;
        private bool _pulse1Enabled;

        // Pulse 2
        private int _pulse2Duty;
        private int _pulse2DutyPos;
        private bool _pulse2LengthHalt;
        private bool _pulse2ConstantVolume;
        private int _pulse2Volume;
        private int _pulse2SweepEnabled;
        private int _pulse2SweepPeriod;
        private bool _pulse2SweepNegate;
        private int _pulse2SweepShift;
        private int _pulse2SweepDivider;
        private bool _pulse2SweepReload;
        private int _pulse2TimerPeriod;
        private int _pulse2TimerValue;
        private int _pulse2LengthCounter;
        private int _pulse2EnvelopeDivider;
        private int _pulse2EnvelopeDecay;
        private bool _pulse2EnvelopeStart;
        private bool _pulse2Enabled;

        // Triangle
        private bool _triangleLengthHalt;
        private int _triangleLinearCounterReload;
        private int _triangleLinearCounter;
        private bool _triangleLinearCounterReloadFlag;
        private int _triangleTimerPeriod;
        private int _triangleTimerValue;
        private int _triangleLengthCounter;
        private int _triangleSequencePos;
        private bool _triangleEnabled;

        // Noise
        private bool _noiseLengthHalt;
        private bool _noiseConstantVolume;
        private int _noiseVolume;
        private bool _noiseMode;
        private int _noisePeriod;
        private int _noiseTimerValue;
        private int _noiseLengthCounter;
        private int _noiseEnvelopeDivider;
        private int _noiseEnvelopeDecay;
        private bool _noiseEnvelopeStart;
        private int _noiseShiftRegister;
        private bool _noiseEnabled;

        // DMC
        private bool _dmcIrqEnabled;
        private bool _dmcLoop;
        private int _dmcRate;
        private int _dmcTimerValue;
        private int _dmcOutputLevel;
        private int _dmcSampleAddress;
        private int _dmcSampleLength;
        private int _dmcCurrentAddress;
        private int _dmcBytesRemaining;
        private int _dmcShiftRegister;
        private int _dmcBitsRemaining;
        private bool _dmcSilenceFlag;
        private bool _dmcIrqFlag;
        private bool _dmcEnabled;
        private byte _dmcSampleBuffer;
        private bool _dmcSampleBufferEmpty;

        // Reference to NES for DMC memory reads
        private Nes _nes;

        // Lookup tables
        private static readonly int[] LengthTable = {
            10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
        };

        private static readonly bool[][] DutyTable = {
            new bool[] { false, true, false, false, false, false, false, false },
            new bool[] { false, true, true, false, false, false, false, false },
            new bool[] { false, true, true, true, true, false, false, false },
            new bool[] { true, false, false, true, true, true, true, true }
        };

        private static readonly int[] TriangleSequence = {
            15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
        };

        private static readonly int[] NoisePeriodTable = {
            4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068
        };

        private static readonly int[] DmcRateTable = {
            428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54
        };

        // Mixer lookup tables
        private static readonly float[] PulseTable;
        private static readonly float[] TndTable;

        static Apu()
        {
            PulseTable = new float[31];
            for (int i = 0; i < 31; i++)
                PulseTable[i] = i == 0 ? 0f : 95.52f / (8128.0f / i + 100.0f);

            TndTable = new float[203];
            for (int i = 0; i < 203; i++)
                TndTable[i] = i == 0 ? 0f : 163.67f / (24329.0f / i + 100.0f);
        }

        public Apu(Nes nes)
        {
            _nes = nes;
            _sampleRate = 44100;
            _cyclesPerSample = (double)CPU_CLOCK / _sampleRate;
            _noiseShiftRegister = 1;
            _dmcSampleBufferEmpty = true;
            _noisePeriod = NoisePeriodTable[0];
            _dmcRate = DmcRateTable[0];
        }

        public void SetSampleRate(int sampleRate)
        {
            _sampleRate = sampleRate;
            _cyclesPerSample = (double)CPU_CLOCK / _sampleRate;
        }

        public void Reset()
        {
            _pulse1Enabled = false;
            _pulse2Enabled = false;
            _triangleEnabled = false;
            _noiseEnabled = false;
            _dmcEnabled = false;
            _pulse1LengthCounter = 0;
            _pulse2LengthCounter = 0;
            _triangleLengthCounter = 0;
            _noiseLengthCounter = 0;
            _noiseShiftRegister = 1;
            _noisePeriod = NoisePeriodTable[0];
            _dmcRate = DmcRateTable[0];
            _frameCounterCycle = 0;
            _frameCounterMode = false;
            _frameIrqInhibit = true;
            _frameIrqFlag = false;
            _dmcIrqFlag = false;
            _dmcBytesRemaining = 0;
            _dmcSampleBufferEmpty = true;
            _dmcOutputLevel = 0;
            _dmcBitsRemaining = 0;
            _dmcSilenceFlag = true;
            _sampleWritePos = 0;
            _sampleReadPos = 0;
            _sampleCycleCounter = 0;
        }

        public void WriteRegister(ushort address, byte value)
        {
            switch (address)
            {
                // Pulse 1
                case 0x4000:
                    _pulse1Duty = (value >> 6) & 3;
                    _pulse1LengthHalt = (value & 0x20) != 0;
                    _pulse1ConstantVolume = (value & 0x10) != 0;
                    _pulse1Volume = value & 0x0F;
                    break;
                case 0x4001:
                    _pulse1SweepEnabled = (value >> 7) & 1;
                    _pulse1SweepPeriod = (value >> 4) & 7;
                    _pulse1SweepNegate = (value & 0x08) != 0;
                    _pulse1SweepShift = value & 7;
                    _pulse1SweepReload = true;
                    break;
                case 0x4002:
                    _pulse1TimerPeriod = (_pulse1TimerPeriod & 0x700) | value;
                    break;
                case 0x4003:
                    _pulse1TimerPeriod = (_pulse1TimerPeriod & 0xFF) | ((value & 7) << 8);
                    if (_pulse1Enabled)
                        _pulse1LengthCounter = LengthTable[(value >> 3) & 0x1F];
                    _pulse1DutyPos = 0;
                    _pulse1EnvelopeStart = true;
                    break;

                // Pulse 2
                case 0x4004:
                    _pulse2Duty = (value >> 6) & 3;
                    _pulse2LengthHalt = (value & 0x20) != 0;
                    _pulse2ConstantVolume = (value & 0x10) != 0;
                    _pulse2Volume = value & 0x0F;
                    break;
                case 0x4005:
                    _pulse2SweepEnabled = (value >> 7) & 1;
                    _pulse2SweepPeriod = (value >> 4) & 7;
                    _pulse2SweepNegate = (value & 0x08) != 0;
                    _pulse2SweepShift = value & 7;
                    _pulse2SweepReload = true;
                    break;
                case 0x4006:
                    _pulse2TimerPeriod = (_pulse2TimerPeriod & 0x700) | value;
                    break;
                case 0x4007:
                    _pulse2TimerPeriod = (_pulse2TimerPeriod & 0xFF) | ((value & 7) << 8);
                    if (_pulse2Enabled)
                        _pulse2LengthCounter = LengthTable[(value >> 3) & 0x1F];
                    _pulse2DutyPos = 0;
                    _pulse2EnvelopeStart = true;
                    break;

                // Triangle
                case 0x4008:
                    _triangleLengthHalt = (value & 0x80) != 0;
                    _triangleLinearCounterReload = value & 0x7F;
                    break;
                case 0x400A:
                    _triangleTimerPeriod = (_triangleTimerPeriod & 0x700) | value;
                    break;
                case 0x400B:
                    _triangleTimerPeriod = (_triangleTimerPeriod & 0xFF) | ((value & 7) << 8);
                    if (_triangleEnabled)
                        _triangleLengthCounter = LengthTable[(value >> 3) & 0x1F];
                    _triangleLinearCounterReloadFlag = true;
                    break;

                // Noise
                case 0x400C:
                    _noiseLengthHalt = (value & 0x20) != 0;
                    _noiseConstantVolume = (value & 0x10) != 0;
                    _noiseVolume = value & 0x0F;
                    break;
                case 0x400E:
                    _noiseMode = (value & 0x80) != 0;
                    _noisePeriod = NoisePeriodTable[value & 0x0F];
                    break;
                case 0x400F:
                    if (_noiseEnabled)
                        _noiseLengthCounter = LengthTable[(value >> 3) & 0x1F];
                    _noiseEnvelopeStart = true;
                    break;

                // DMC
                case 0x4010:
                    _dmcIrqEnabled = (value & 0x80) != 0;
                    _dmcLoop = (value & 0x40) != 0;
                    _dmcRate = DmcRateTable[value & 0x0F];
                    if (!_dmcIrqEnabled)
                        _dmcIrqFlag = false;
                    break;
                case 0x4011:
                    _dmcOutputLevel = value & 0x7F;
                    break;
                case 0x4012:
                    _dmcSampleAddress = 0xC000 + value * 64;
                    break;
                case 0x4013:
                    _dmcSampleLength = value * 16 + 1;
                    break;

                // Status
                case 0x4015:
                    _pulse1Enabled = (value & 1) != 0;
                    _pulse2Enabled = (value & 2) != 0;
                    _triangleEnabled = (value & 4) != 0;
                    _noiseEnabled = (value & 8) != 0;
                    _dmcEnabled = (value & 16) != 0;

                    if (!_pulse1Enabled) _pulse1LengthCounter = 0;
                    if (!_pulse2Enabled) _pulse2LengthCounter = 0;
                    if (!_triangleEnabled) _triangleLengthCounter = 0;
                    if (!_noiseEnabled) _noiseLengthCounter = 0;

                    if (!_dmcEnabled)
                    {
                        _dmcBytesRemaining = 0;
                    }
                    else if (_dmcBytesRemaining == 0)
                    {
                        _dmcCurrentAddress = _dmcSampleAddress;
                        _dmcBytesRemaining = _dmcSampleLength;
                    }
                    _dmcIrqFlag = false;
                    break;

                // Frame counter
                case 0x4017:
                    _frameCounterMode = (value & 0x80) != 0;
                    _frameIrqInhibit = (value & 0x40) != 0;
                    if (_frameIrqInhibit)
                        _frameIrqFlag = false;
                    _frameCounterCycle = 0;
                    if (_frameCounterMode)
                    {
                        ClockQuarterFrame();
                        ClockHalfFrame();
                    }
                    break;
            }
        }

        public byte ReadStatus()
        {
            byte status = 0;
            if (_pulse1LengthCounter > 0) status |= 1;
            if (_pulse2LengthCounter > 0) status |= 2;
            if (_triangleLengthCounter > 0) status |= 4;
            if (_noiseLengthCounter > 0) status |= 8;
            if (_dmcBytesRemaining > 0) status |= 16;
            if (_frameIrqFlag) status |= 0x40;
            if (_dmcIrqFlag) status |= 0x80;
            _frameIrqFlag = false;
            return status;
        }

        public void Step()
        {
            _frameCounterCycle++;

            if (!_frameCounterMode)
            {
                // 4-step mode
                switch (_frameCounterCycle)
                {
                    case 3729:
                        ClockQuarterFrame();
                        break;
                    case 7457:
                        ClockQuarterFrame();
                        ClockHalfFrame();
                        break;
                    case 11186:
                        ClockQuarterFrame();
                        break;
                    case 14915:
                        ClockQuarterFrame();
                        ClockHalfFrame();
                        if (!_frameIrqInhibit)
                            _frameIrqFlag = true;
                        _frameCounterCycle = 0;
                        break;
                }
            }
            else
            {
                // 5-step mode
                switch (_frameCounterCycle)
                {
                    case 3729:
                        ClockQuarterFrame();
                        break;
                    case 7457:
                        ClockQuarterFrame();
                        ClockHalfFrame();
                        break;
                    case 11186:
                        ClockQuarterFrame();
                        break;
                    case 18641:
                        ClockQuarterFrame();
                        ClockHalfFrame();
                        _frameCounterCycle = 0;
                        break;
                }
            }

            // Triangle clocks every CPU cycle
            ClockTriangle();

            // Pulse, noise, DMC clock every other CPU cycle
            if ((_frameCounterCycle & 1) == 0)
            {
                ClockPulse1();
                ClockPulse2();
                ClockNoise();
                ClockDmc();
            }

            // Generate sample
            _sampleCycleCounter++;
            if (_sampleCycleCounter >= _cyclesPerSample)
            {
                _sampleCycleCounter -= _cyclesPerSample;
                OutputSample();
            }
        }

        private void ClockQuarterFrame()
        {
            ClockEnvelope(ref _pulse1EnvelopeDivider, ref _pulse1EnvelopeDecay,
                ref _pulse1EnvelopeStart, _pulse1Volume, _pulse1LengthHalt);
            ClockEnvelope(ref _pulse2EnvelopeDivider, ref _pulse2EnvelopeDecay,
                ref _pulse2EnvelopeStart, _pulse2Volume, _pulse2LengthHalt);
            ClockEnvelope(ref _noiseEnvelopeDivider, ref _noiseEnvelopeDecay,
                ref _noiseEnvelopeStart, _noiseVolume, _noiseLengthHalt);

            // Triangle linear counter
            if (_triangleLinearCounterReloadFlag)
                _triangleLinearCounter = _triangleLinearCounterReload;
            else if (_triangleLinearCounter > 0)
                _triangleLinearCounter--;

            if (!_triangleLengthHalt)
                _triangleLinearCounterReloadFlag = false;
        }

        private void ClockHalfFrame()
        {
            if (!_pulse1LengthHalt && _pulse1LengthCounter > 0) _pulse1LengthCounter--;
            if (!_pulse2LengthHalt && _pulse2LengthCounter > 0) _pulse2LengthCounter--;
            if (!_triangleLengthHalt && _triangleLengthCounter > 0) _triangleLengthCounter--;
            if (!_noiseLengthHalt && _noiseLengthCounter > 0) _noiseLengthCounter--;

            ClockSweep1();
            ClockSweep2();
        }

        private void ClockEnvelope(ref int divider, ref int decay, ref bool start, int volume, bool loop)
        {
            if (start)
            {
                start = false;
                decay = 15;
                divider = volume;
            }
            else
            {
                if (divider > 0)
                {
                    divider--;
                }
                else
                {
                    divider = volume;
                    if (decay > 0)
                        decay--;
                    else if (loop)
                        decay = 15;
                }
            }
        }

        private void ClockSweep1()
        {
            if (_pulse1SweepDivider == 0 && _pulse1SweepEnabled != 0
                && _pulse1SweepShift > 0 && !IsPulse1Muted())
            {
                int change = _pulse1TimerPeriod >> _pulse1SweepShift;
                if (_pulse1SweepNegate)
                    change = -change - 1; // One's complement (pulse 1)
                _pulse1TimerPeriod += change;
            }

            if (_pulse1SweepDivider == 0 || _pulse1SweepReload)
            {
                _pulse1SweepDivider = _pulse1SweepPeriod;
                _pulse1SweepReload = false;
            }
            else
            {
                _pulse1SweepDivider--;
            }
        }

        private void ClockSweep2()
        {
            if (_pulse2SweepDivider == 0 && _pulse2SweepEnabled != 0
                && _pulse2SweepShift > 0 && !IsPulse2Muted())
            {
                int change = _pulse2TimerPeriod >> _pulse2SweepShift;
                if (_pulse2SweepNegate)
                    change = -change; // Two's complement (pulse 2)
                _pulse2TimerPeriod += change;
            }

            if (_pulse2SweepDivider == 0 || _pulse2SweepReload)
            {
                _pulse2SweepDivider = _pulse2SweepPeriod;
                _pulse2SweepReload = false;
            }
            else
            {
                _pulse2SweepDivider--;
            }
        }

        private bool IsPulse1Muted()
        {
            int target = _pulse1TimerPeriod + (_pulse1TimerPeriod >> _pulse1SweepShift);
            return _pulse1TimerPeriod < 8 || (!_pulse1SweepNegate && target > 0x7FF);
        }

        private bool IsPulse2Muted()
        {
            int target = _pulse2TimerPeriod + (_pulse2TimerPeriod >> _pulse2SweepShift);
            return _pulse2TimerPeriod < 8 || (!_pulse2SweepNegate && target > 0x7FF);
        }

        private void ClockPulse1()
        {
            if (_pulse1TimerValue == 0)
            {
                _pulse1TimerValue = _pulse1TimerPeriod;
                _pulse1DutyPos = (_pulse1DutyPos + 1) & 7;
            }
            else
            {
                _pulse1TimerValue--;
            }
        }

        private void ClockPulse2()
        {
            if (_pulse2TimerValue == 0)
            {
                _pulse2TimerValue = _pulse2TimerPeriod;
                _pulse2DutyPos = (_pulse2DutyPos + 1) & 7;
            }
            else
            {
                _pulse2TimerValue--;
            }
        }

        private void ClockTriangle()
        {
            if (_triangleTimerValue == 0)
            {
                _triangleTimerValue = _triangleTimerPeriod;
                if (_triangleLengthCounter > 0 && _triangleLinearCounter > 0)
                    _triangleSequencePos = (_triangleSequencePos + 1) & 31;
            }
            else
            {
                _triangleTimerValue--;
            }
        }

        private void ClockNoise()
        {
            if (_noiseTimerValue == 0)
            {
                _noiseTimerValue = _noisePeriod;
                int feedback;
                if (_noiseMode)
                    feedback = (_noiseShiftRegister & 1) ^ ((_noiseShiftRegister >> 6) & 1);
                else
                    feedback = (_noiseShiftRegister & 1) ^ ((_noiseShiftRegister >> 1) & 1);
                _noiseShiftRegister = (_noiseShiftRegister >> 1) | (feedback << 14);
            }
            else
            {
                _noiseTimerValue--;
            }
        }

        private void ClockDmc()
        {
            // Fill sample buffer if empty
            if (_dmcSampleBufferEmpty && _dmcBytesRemaining > 0)
            {
                _dmcSampleBuffer = _nes.CpuRead((ushort)_dmcCurrentAddress);
                _dmcSampleBufferEmpty = false;
                _dmcCurrentAddress = ((_dmcCurrentAddress + 1) & 0xFFFF) | 0x8000;
                _dmcBytesRemaining--;

                if (_dmcBytesRemaining == 0)
                {
                    if (_dmcLoop)
                    {
                        _dmcCurrentAddress = _dmcSampleAddress;
                        _dmcBytesRemaining = _dmcSampleLength;
                    }
                    else if (_dmcIrqEnabled)
                    {
                        _dmcIrqFlag = true;
                    }
                }
            }

            if (_dmcTimerValue == 0)
            {
                _dmcTimerValue = _dmcRate;

                if (!_dmcSilenceFlag)
                {
                    if ((_dmcShiftRegister & 1) != 0)
                    {
                        if (_dmcOutputLevel <= 125)
                            _dmcOutputLevel += 2;
                    }
                    else
                    {
                        if (_dmcOutputLevel >= 2)
                            _dmcOutputLevel -= 2;
                    }
                    _dmcShiftRegister >>= 1;
                }

                _dmcBitsRemaining--;
                if (_dmcBitsRemaining <= 0)
                {
                    _dmcBitsRemaining = 8;
                    if (_dmcSampleBufferEmpty)
                    {
                        _dmcSilenceFlag = true;
                    }
                    else
                    {
                        _dmcSilenceFlag = false;
                        _dmcShiftRegister = _dmcSampleBuffer;
                        _dmcSampleBufferEmpty = true;
                    }
                }
            }
            else
            {
                _dmcTimerValue--;
            }
        }

        private void OutputSample()
        {
            int pulse1 = GetPulse1Output();
            int pulse2 = GetPulse2Output();
            int triangle = GetTriangleOutput();
            int noise = GetNoiseOutput();
            int dmc = _dmcOutputLevel;

            float pulseOut = PulseTable[pulse1 + pulse2];
            int tndIndex = 3 * triangle + 2 * noise + dmc;
            if (tndIndex >= TndTable.Length) tndIndex = TndTable.Length - 1;
            float tndOut = TndTable[tndIndex];

            float sample = pulseOut + tndOut;

            // Write to ring buffer
            int nextWrite = (_sampleWritePos + 1) % SAMPLE_BUFFER_SIZE;
            if (nextWrite != _sampleReadPos)
            {
                _sampleBuffer[_sampleWritePos] = sample;
                _sampleWritePos = nextWrite;
            }
        }

        private int GetPulse1Output()
        {
            if (!_pulse1Enabled || _pulse1LengthCounter == 0 || IsPulse1Muted())
                return 0;
            if (!DutyTable[_pulse1Duty][_pulse1DutyPos])
                return 0;
            return _pulse1ConstantVolume ? _pulse1Volume : _pulse1EnvelopeDecay;
        }

        private int GetPulse2Output()
        {
            if (!_pulse2Enabled || _pulse2LengthCounter == 0 || IsPulse2Muted())
                return 0;
            if (!DutyTable[_pulse2Duty][_pulse2DutyPos])
                return 0;
            return _pulse2ConstantVolume ? _pulse2Volume : _pulse2EnvelopeDecay;
        }

        private int GetTriangleOutput()
        {
            if (!_triangleEnabled || _triangleLengthCounter == 0 || _triangleLinearCounter == 0)
                return 0;
            if (_triangleTimerPeriod < 2)
                return 7; // Mute ultrasonic, return midpoint
            return TriangleSequence[_triangleSequencePos];
        }

        private int GetNoiseOutput()
        {
            if (!_noiseEnabled || _noiseLengthCounter == 0)
                return 0;
            if ((_noiseShiftRegister & 1) != 0)
                return 0;
            return _noiseConstantVolume ? _noiseVolume : _noiseEnvelopeDecay;
        }

        // Called from Unity audio thread via OnAudioFilterRead
        public int ReadSamples(float[] buffer, int count)
        {
            int samplesRead = 0;
            for (int i = 0; i < count; i++)
            {
                if (_sampleReadPos != _sampleWritePos)
                {
                    buffer[i] = _sampleBuffer[_sampleReadPos];
                    _sampleReadPos = (_sampleReadPos + 1) % SAMPLE_BUFFER_SIZE;
                    samplesRead++;
                }
                else
                {
                    buffer[i] = 0f;
                }
            }
            return samplesRead;
        }

        public bool IrqPending => _frameIrqFlag || _dmcIrqFlag;
    }
}
