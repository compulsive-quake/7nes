namespace SevenNes.Core
{
    public class Controller
    {
        private byte _buttonState;
        private byte _shiftRegister;
        private bool _strobe;

        // Button indices: 0=A, 1=B, 2=Select, 3=Start, 4=Up, 5=Down, 6=Left, 7=Right

        public void SetButtonState(byte state)
        {
            _buttonState = state;
        }

        public void SetButton(int button, bool pressed)
        {
            if (pressed)
                _buttonState |= (byte)(1 << button);
            else
                _buttonState &= (byte)~(1 << button);
        }

        public void Write(byte value)
        {
            _strobe = (value & 1) == 1;
            if (_strobe)
                _shiftRegister = _buttonState;
        }

        public byte Read()
        {
            byte result = (byte)(_shiftRegister & 1);
            _shiftRegister >>= 1;
            if (_strobe)
                _shiftRegister = _buttonState;
            return result;
        }
    }
}
