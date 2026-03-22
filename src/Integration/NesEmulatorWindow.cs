using System;
using System.IO;
using UnityEngine;

namespace SevenNes.Integration
{
    public class NesEmulatorWindow : MonoBehaviour
    {
        private static NesEmulatorWindow _instance;
        private bool _isOpen;
        private bool _showRomList;
        private Vector2 _romListScroll;
        private NesEmulatorManager _manager;
        private GUIStyle _screenStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _romButtonStyle;
        private bool _stylesInitialized;

        public static NesEmulatorWindow Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("NesEmulatorWindow");
                    _instance = go.AddComponent<NesEmulatorWindow>();
                    GameObject.DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        void Awake()
        {
            _manager = NesEmulatorManager.Instance;
        }

        public void Open()
        {
            _isOpen = true;
            _showRomList = !_manager.IsRunning;

            // Lock player controls
            var playerLocal = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (playerLocal != null)
            {
                playerLocal.SetControllable(false);
            }

            // Show and unlock cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            _isOpen = false;
            _showRomList = false;
            _manager.Stop();

            // Restore player controls
            var playerLocal = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (playerLocal != null)
            {
                playerLocal.SetControllable(true);
            }

            // Re-lock cursor for gameplay
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        void InitStyles()
        {
            if (_stylesInitialized) return;

            _screenStyle = new GUIStyle();
            _screenStyle.normal.background = Texture2D.blackTexture;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 16;
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.normal.textColor = Color.white;

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 14;
            _buttonStyle.padding = new RectOffset(10, 10, 5, 5);

            _romButtonStyle = new GUIStyle(GUI.skin.button);
            _romButtonStyle.fontSize = 14;
            _romButtonStyle.alignment = TextAnchor.MiddleLeft;
            _romButtonStyle.padding = new RectOffset(15, 15, 8, 8);

            _stylesInitialized = true;
        }

        void Update()
        {
            if (!_isOpen) return;

            // Handle escape to close
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (_showRomList) return;

            // Handle Tab to show ROM list
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _showRomList = true;
                return;
            }

            // Handle F5 to reset
            if (Input.GetKeyDown(KeyCode.F5))
            {
                _manager.Reset();
                return;
            }

            // Map keyboard to NES controller
            // 0=A, 1=B, 2=Select, 3=Start, 4=Up, 5=Down, 6=Left, 7=Right
            _manager.SetButton(0, Input.GetKey(KeyCode.Z));         // A
            _manager.SetButton(1, Input.GetKey(KeyCode.X));         // B
            _manager.SetButton(2, Input.GetKey(KeyCode.RightShift));// Select
            _manager.SetButton(3, Input.GetKey(KeyCode.Return));    // Start
            _manager.SetButton(4, Input.GetKey(KeyCode.UpArrow));   // Up
            _manager.SetButton(5, Input.GetKey(KeyCode.DownArrow)); // Down
            _manager.SetButton(6, Input.GetKey(KeyCode.LeftArrow)); // Left
            _manager.SetButton(7, Input.GetKey(KeyCode.RightArrow));// Right

            // Run emulator frame
            _manager.RunFrame();
        }

        void OnGUI()
        {
            if (!_isOpen) return;

            InitStyles();

            // Full screen dark background
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _screenStyle);

            if (_showRomList)
            {
                DrawRomList();
            }
            else
            {
                DrawEmulatorScreen();
            }
        }

        void DrawEmulatorScreen()
        {
            // Calculate screen size maintaining 256:240 aspect ratio (roughly 16:15)
            float aspect = 256f / 240f;
            float maxHeight = Screen.height * 0.85f;
            float maxWidth = Screen.width * 0.85f;

            float height = maxHeight;
            float width = height * aspect;

            if (width > maxWidth)
            {
                width = maxWidth;
                height = width / aspect;
            }

            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f - 20;

            // Draw NES screen
            if (_manager.ScreenTexture != null)
            {
                GUI.DrawTexture(new Rect(x, y, width, height), _manager.ScreenTexture, ScaleMode.StretchToFill);
            }

            // Draw ROM name and controls help
            GUI.Label(new Rect(0, y + height + 10, Screen.width, 30), _manager.CurrentRomName, _labelStyle);

            var helpStyle = new GUIStyle(_labelStyle);
            helpStyle.fontSize = 12;
            helpStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(0, y + height + 35, Screen.width, 25),
                "Arrows=D-Pad | Z=A | X=B | Enter=Start | RShift=Select | Tab=ROM List | F5=Reset | Esc=Close", helpStyle);
        }

        void DrawRomList()
        {
            float panelWidth = 500;
            float panelHeight = Screen.height * 0.7f;
            float x = (Screen.width - panelWidth) / 2f;
            float y = (Screen.height - panelHeight) / 2f;

            // Panel background
            GUI.Box(new Rect(x - 10, y - 10, panelWidth + 20, panelHeight + 20), "");

            // Title
            var titleStyle = new GUIStyle(_labelStyle);
            titleStyle.fontSize = 24;
            titleStyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(x, y, panelWidth, 40), "NES ROM Library", titleStyle);

            y += 50;
            panelHeight -= 60;

            var romFiles = _manager.GetRomList();

            if (romFiles == null || romFiles.Length == 0)
            {
                var infoStyle = new GUIStyle(_labelStyle);
                infoStyle.wordWrap = true;
                GUI.Label(new Rect(x, y, panelWidth, 100),
                    $"No ROMs found!\n\nPlace .nes ROM files in:\n{ModInit.RomsPath}", infoStyle);
            }
            else
            {
                // Scrollable ROM list
                _romListScroll = GUI.BeginScrollView(
                    new Rect(x, y, panelWidth, panelHeight - 50),
                    _romListScroll,
                    new Rect(0, 0, panelWidth - 20, romFiles.Length * 40));

                for (int i = 0; i < romFiles.Length; i++)
                {
                    string romName = Path.GetFileNameWithoutExtension(romFiles[i]);
                    if (GUI.Button(new Rect(0, i * 40, panelWidth - 20, 35), romName, _romButtonStyle))
                    {
                        if (_manager.LoadRom(i))
                        {
                            _showRomList = false;
                        }
                    }
                }

                GUI.EndScrollView();
            }

            // Close button
            if (GUI.Button(new Rect(x + panelWidth - 100, y + panelHeight - 40, 100, 35), "Close", _buttonStyle))
            {
                if (_manager.IsRunning)
                    _showRomList = false;
                else
                    Close();
            }
        }
    }
}
