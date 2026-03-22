namespace SevenNes.Core
{
    public interface IMapper
    {
        byte CpuRead(ushort address);
        void CpuWrite(ushort address, byte value);
        byte PpuRead(ushort address);
        void PpuWrite(ushort address, byte value);
    }
}
