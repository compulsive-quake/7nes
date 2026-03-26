using System;
using System.IO;
using UnityEngine;

namespace SevenNes.Integration
{
    public class NesInputBindings
    {
        private static NesInputBindings _instance;
        public static NesInputBindings Instance => _instance ??= new NesInputBindings();

        // Button indices: 0=A, 1=B, 2=Select, 3=Start, 4=Up, 5=Down, 6=Left, 7=Right
        public static readonly string[] ButtonNames = { "A", "B", "Select", "Start", "Up", "Down", "Left", "Right" };
        public const int ButtonCount = 8;

        private KeyCode[] _keyboardBindings;
        private KeyCode[] _gamepadBindings;

        // Extra bindings (non-NES-controller keys)
        private KeyCode _fullscreenKey;
        private static readonly KeyCode DefaultFullscreenKey = KeyCode.U;
        public KeyCode FullscreenKey { get => _fullscreenKey; set => _fullscreenKey = value; }

        private static readonly KeyCode[] DefaultKeyboard = {
            KeyCode.A,          // A
            KeyCode.D,          // B
            KeyCode.RightShift, // Select
            KeyCode.Return,     // Start
            KeyCode.UpArrow,    // Up
            KeyCode.DownArrow,  // Down
            KeyCode.LeftArrow,  // Left
            KeyCode.RightArrow  // Right
        };

        private static readonly KeyCode[] DefaultGamepad = {
            KeyCode.JoystickButton0,  // A
            KeyCode.JoystickButton1,  // B
            KeyCode.JoystickButton6,  // Select (Back)
            KeyCode.JoystickButton7,  // Start
            KeyCode.JoystickButton13, // Up (D-pad up)
            KeyCode.JoystickButton14, // Down (D-pad down)
            KeyCode.JoystickButton11, // Left (D-pad left)
            KeyCode.JoystickButton12  // Right (D-pad right)
        };

        private string _savePath;

        public NesInputBindings()
        {
            _keyboardBindings = (KeyCode[])DefaultKeyboard.Clone();
            _gamepadBindings = (KeyCode[])DefaultGamepad.Clone();
            _fullscreenKey = DefaultFullscreenKey;
        }

        public void SetSavePath(string modPath)
        {
            _savePath = Path.Combine(modPath, "controls.cfg");
            Load();
        }

        public KeyCode GetKeyboard(int button) => _keyboardBindings[button];
        public KeyCode GetGamepad(int button) => _gamepadBindings[button];
        public void SetKeyboard(int button, KeyCode key) => _keyboardBindings[button] = key;
        public void SetGamepad(int button, KeyCode key) => _gamepadBindings[button] = key;

        public bool IsPressed(int button)
        {
            if (_keyboardBindings[button] != KeyCode.None && Input.GetKey(_keyboardBindings[button])) return true;
            if (_gamepadBindings[button] != KeyCode.None && Input.GetKey(_gamepadBindings[button])) return true;
            return false;
        }

        public void ResetDefaults()
        {
            Array.Copy(DefaultKeyboard, _keyboardBindings, ButtonCount);
            Array.Copy(DefaultGamepad, _gamepadBindings, ButtonCount);
            _fullscreenKey = DefaultFullscreenKey;
        }

        public void Save()
        {
            if (_savePath == null) return;
            try
            {
                using (var writer = new StreamWriter(_savePath))
                {
                    for (int i = 0; i < ButtonCount; i++)
                    {
                        writer.WriteLine($"{ButtonNames[i]}_kb={_keyboardBindings[i]}");
                        writer.WriteLine($"{ButtonNames[i]}_gp={_gamepadBindings[i]}");
                    }
                    writer.WriteLine($"Fullscreen_kb={_fullscreenKey}");
                }
                Log.Out("[7nes] Controls saved");
            }
            catch (Exception e)
            {
                Log.Warning($"[7nes] Failed to save controls: {e.Message}");
            }
        }

        public void Load()
        {
            if (_savePath == null || !File.Exists(_savePath)) return;
            try
            {
                var lines = File.ReadAllLines(_savePath);
                foreach (var line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    if (!Enum.TryParse(val, out KeyCode keyCode)) continue;

                    if (key == "Fullscreen_kb")
                    {
                        _fullscreenKey = keyCode;
                        continue;
                    }

                    for (int i = 0; i < ButtonCount; i++)
                    {
                        if (key == ButtonNames[i] + "_kb")
                            _keyboardBindings[i] = keyCode;
                        else if (key == ButtonNames[i] + "_gp")
                            _gamepadBindings[i] = keyCode;
                    }
                }
                Log.Out("[7nes] Controls loaded");
            }
            catch (Exception e)
            {
                Log.Warning($"[7nes] Failed to load controls: {e.Message}");
            }
        }

        public string GetControlsHintString()
        {
            string dpad = $"{KeyName(GetKeyboard(4))}/{KeyName(GetKeyboard(5))}/{KeyName(GetKeyboard(6))}/{KeyName(GetKeyboard(7))}";
            string a = KeyName(GetKeyboard(0));
            string b = KeyName(GetKeyboard(1));
            string start = KeyName(GetKeyboard(3));
            string select = KeyName(GetKeyboard(2));
            string fullscreen = KeyName(_fullscreenKey);
            return $"{dpad}=D-Pad | {a}=A | {b}=B | {start}=Start | {select}=Select | {fullscreen}=Fullscreen | F5=Reset | E=Quit";
        }

        public static string KeyName(KeyCode key)
        {
            if (key == KeyCode.None) return "---";
            switch (key)
            {
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.LeftShift: return "LShift";
                case KeyCode.RightShift: return "RShift";
                case KeyCode.LeftControl: return "LCtrl";
                case KeyCode.RightControl: return "RCtrl";
                case KeyCode.LeftAlt: return "LAlt";
                case KeyCode.RightAlt: return "RAlt";
                case KeyCode.Return: return "Enter";
                case KeyCode.Space: return "Space";
                case KeyCode.Backspace: return "BkSp";
                case KeyCode.Delete: return "Del";
                case KeyCode.Insert: return "Ins";
                case KeyCode.CapsLock: return "Caps";
                case KeyCode.Tab: return "Tab";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                default:
                    string s = key.ToString();
                    if (s.StartsWith("JoystickButton"))
                        return "Joy " + s.Substring(14);
                    if (s.StartsWith("Joystick") && s.Contains("Button"))
                    {
                        int btnIdx = s.IndexOf("Button");
                        return "Joy" + s.Substring(8, btnIdx - 8) + " " + s.Substring(btnIdx + 6);
                    }
                    if (s.StartsWith("Keypad"))
                        return "Num" + s.Substring(6);
                    if (s.StartsWith("Alpha"))
                        return s.Substring(5);
                    return s;
            }
        }

        /// <summary>
        /// Detect any key/button press. Returns KeyCode.None if nothing pressed.
        /// </summary>
        public static KeyCode DetectKeyDown(bool gamepadMode)
        {
            if (gamepadMode)
            {
                // JoystickButton0 (330) through Joystick8Button19 (509)
                for (int i = 330; i <= 509; i++)
                {
                    KeyCode k = (KeyCode)i;
                    if (Input.GetKeyDown(k)) return k;
                }
            }
            else
            {
                // Keyboard keys: Backspace(8) through ScrollLock(302), skip mouse buttons (323-329)
                for (int i = 8; i <= 322; i++)
                {
                    if (i == (int)KeyCode.Escape) continue;
                    KeyCode k = (KeyCode)i;
                    if (Input.GetKeyDown(k)) return k;
                }
            }
            return KeyCode.None;
        }
    }
}
