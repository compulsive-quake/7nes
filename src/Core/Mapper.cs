namespace SevenNes.Core
{
    public interface IMapper
    {
        byte CpuRead(ushort address);
        void CpuWrite(ushort address, byte value);
        byte PpuRead(ushort address);
        void PpuWrite(ushort address, byte value);
        void NotifyScanline();
        /// <summary>
        /// Called once per CPU cycle for mappers that use cycle-based IRQ counters (e.g. FME-7).
        /// </summary>
        void NotifyCpuCycle();
    }
}
