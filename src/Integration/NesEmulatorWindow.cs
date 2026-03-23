using System.IO;
using UnityEngine;

namespace SevenNes.Integration
{
    public class NesEmulatorWindow : MonoBehaviour
    {
        private static NesEmulatorWindow _instance;

        // === STATE ===
        private enum UIState { Closed, RomList, Playing }
        private UIState _uiState = UIState.Closed;
        private bool _tvOn; // TV is powered on (screen quad visible, emulator runs in background)

        private NesEmulatorManager _manager;
        private Vector2 _romListScroll;

        // GUI styles
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _romButtonStyle;
        private GUIStyle _panelStyle;
        private bool _stylesInitialized;

        // In-world screen rendering
        private GameObject _screenQuad;
        private Material _screenMaterial;
        private Texture2D _dimOverlay;

        // Per-rotation calibration: [normalOffset, verticalOffset, screenWidth] for each yaw (0-3)
        private static readonly float[,] DefaultCalibration = new float[,]
        {
            { 0.250f, 0.080f, 0.870f }, // Rotation 0 (North)
            { 0.250f, 0.080f, 0.870f }, // Rotation 1 (East)
            { 0.250f, 0.080f, 0.870f }, // Rotation 2 (South)
            { 0.250f, 0.080f, 0.870f }, // Rotation 3 (West)
        };

        // Per-rotation horizontal flip (true = mirror the texture)
        private static readonly bool[] DefaultFlip = { false, false, false, true };

        // Active calibration values
        private float[,] _calibration;
        private bool[] _flipHorizontal;
        private int _currentYaw;
        private Vector3 _blockCenter;
        private Vector3 _screenNormal;

        private const float OffsetStep = 0.01f;
        private const float ScaleStep = 0.02f;

        // Block info for re-creating quad
        private Vector3i _blockPos;
        private byte _blockRotation;


        // === SINGLETON ===
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

        // === INITIALIZATION ===
        void Awake()
        {
            _manager = NesEmulatorManager.Instance;
            _dimOverlay = new Texture2D(1, 1);
            _dimOverlay.SetPixel(0, 0, new Color(0, 0, 0, 0.75f));
            _dimOverlay.Apply();

            _calibration = new float[4, 3];
            _flipHorizontal = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                    _calibration[i, j] = DefaultCalibration[i, j];
                _flipHorizontal[i] = DefaultFlip[i];
            }

        }

        // === PUBLIC PROPERTIES (used by Harmony patches for dynamic command state) ===
        public bool IsTvOn => _tvOn;
        public bool HasLoadedRom => _manager != null && _manager.HasLoadedRom;

        // === PUBLIC API (called from Harmony patches) ===

        /// Default E press: play if ROM loaded, otherwise open ROM list
        public void HandleActivate(Vector3i blockPos, byte rotation)
        {
            if (_uiState == UIState.Playing) return;
            SetBlockInfo(blockPos, rotation);

            if (_manager.HasLoadedRom)
            {
                if (!_manager.IsRunning)
                    _manager.Resume();
                _tvOn = true;
                EnsureScreenQuad();
                SetUIState(UIState.Playing);
            }
            else
            {
                // No ROM loaded — go to ROM list
                _romListScroll = Vector2.zero;
                SetUIState(UIState.RomList);
            }
        }

        /// Hold-E radial: Choose Game
        public void HandleChooseGame(Vector3i blockPos, byte rotation)
        {
            if (_uiState == UIState.Playing) return;
            SetBlockInfo(blockPos, rotation);
            _romListScroll = Vector2.zero;
            SetUIState(UIState.RomList);
        }

        /// Hold-E radial: Turn On
        public void HandleTurnOn(Vector3i blockPos, byte rotation)
        {
            if (_uiState == UIState.Playing) return;
            SetBlockInfo(blockPos, rotation);

            if (_manager.HasLoadedRom)
            {
                _manager.Resume();
                _tvOn = true;
                EnsureScreenQuad();
                ClearControllerInput();
                Log.Out("[7nes] TV turned on (background)");
            }
            SetUIState(UIState.Closed);
        }

        /// Hold-E radial: Turn Off
        public void HandleTurnOff(Vector3i blockPos, byte rotation)
        {
            if (_uiState == UIState.Playing) return;
            SetBlockInfo(blockPos, rotation);

            _tvOn = false;
            _manager.Stop();
            ClearControllerInput();
            DestroyScreenQuad();
            Log.Out("[7nes] TV turned off");
            SetUIState(UIState.Closed);
        }

        private void SetBlockInfo(Vector3i blockPos, byte rotation)
        {
            _blockPos = blockPos;
            _blockRotation = rotation;
            _currentYaw = rotation & 0x3;
            _blockCenter = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
            _screenNormal = GetScreenNormal(rotation);
        }

        // === STATE MANAGEMENT ===
        private void SetUIState(UIState newState)
        {
            _uiState = newState;
            switch (newState)
            {
                case UIState.Closed:
                    UnlockPlayer();
                    SetHUDVisible(true);
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    break;
                case UIState.RomList:
                    LockPlayer();
                    SetHUDVisible(false);
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    break;
                case UIState.Playing:
                    LockPlayer();
                    SetHUDVisible(false);
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    break;
            }
        }

        private void LockPlayer()
        {
            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player != null) player.SetControllable(false);
        }

        private void UnlockPlayer()
        {
            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player != null) player.SetControllable(true);
        }

        private GUIWindowManager.HudEnabledStates _previousHudState = GUIWindowManager.HudEnabledStates.Enabled;

        private void SetHUDVisible(bool visible)
        {
            try
            {
                var player = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (player == null) return;
                var playerUI = LocalPlayerUI.GetUIForPlayer(player);
                if (playerUI?.windowManager == null) return;

                if (!visible)
                {
                    _previousHudState = playerUI.windowManager.bHUDEnabled;
                    playerUI.windowManager.SetHUDEnabled(GUIWindowManager.HudEnabledStates.FullHide);
                }
                else
                {
                    playerUI.windowManager.SetHUDEnabled(_previousHudState);
                }
            }
            catch (System.Exception e)
            {
                Log.Warning($"[7nes] Could not toggle HUD: {e.Message}");
            }
        }

        private void ClearControllerInput()
        {
            for (int i = 0; i < 8; i++)
                _manager.SetButton(i, false);
        }

        // === SCREEN QUAD ===
        private void EnsureScreenQuad()
        {
            if (_screenQuad != null) return;
            CreateScreenQuad();
        }

        private void CreateScreenQuad()
        {
            DestroyScreenQuad();

            _screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _screenQuad.name = "NesScreen";

            var collider = _screenQuad.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Shader shader = Shader.Find("Unlit/Texture")
                         ?? Shader.Find("UI/Default")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard");
            if (shader == null)
            {
                Log.Warning("[7nes] Could not find any suitable shader for NES screen quad");
                DestroyScreenQuad();
                return;
            }
            _screenMaterial = new Material(shader);
            _screenMaterial.mainTexture = _manager.ScreenTexture;
            _screenQuad.GetComponent<Renderer>().material = _screenMaterial;

            _screenQuad.transform.rotation = Quaternion.LookRotation(-_screenNormal, Vector3.up);
            UpdateQuadTransform();
        }

        private void UpdateQuadTransform()
        {
            if (_screenQuad == null) return;

            float normalOffset = _calibration[_currentYaw, 0];
            float verticalOffset = _calibration[_currentYaw, 1];
            float screenWidth = _calibration[_currentYaw, 2];

            Vector3 screenCenter = _blockCenter + _screenNormal * normalOffset + Vector3.up * verticalOffset;
            _screenQuad.transform.position = screenCenter;

            float screenHeight = screenWidth * (240f / 256f);
            float xScale = _flipHorizontal[_currentYaw] ? -screenWidth : screenWidth;
            _screenQuad.transform.localScale = new Vector3(xScale, screenHeight, 1f);
        }

        private void DestroyScreenQuad()
        {
            if (_screenQuad != null) { Destroy(_screenQuad); _screenQuad = null; }
            if (_screenMaterial != null) { Destroy(_screenMaterial); _screenMaterial = null; }
        }

        private static Vector3 GetScreenNormal(byte rotation)
        {
            int yaw = rotation & 0x3;
            switch (yaw)
            {
                case 0: return new Vector3(0, 0, 1);
                case 1: return new Vector3(-1, 0, 0);
                case 2: return new Vector3(0, 0, -1);
                case 3: return new Vector3(1, 0, 0);
                default: return new Vector3(0, 0, 1);
            }
        }

        // === UPDATE ===
        void Update()
        {
            // Background mode: keep running emulator frames even when UI is closed
            if (_tvOn && _manager.IsRunning && _uiState == UIState.Closed)
            {
                _manager.RunFrame();
                return;
            }

            if (_uiState == UIState.Closed) return;

            // E closes play mode, Escape closes ROM list
            if (_uiState == UIState.Playing && Input.GetKeyDown(KeyCode.E))
            {
                // Exit play mode — TV stays on in background
                ClearControllerInput();
                _tvOn = true;
                Log.Out($"[7nes-calibrate] FINAL VALUES (yaw {_currentYaw}): normalOffset={_calibration[_currentYaw, 0]:F3}  verticalOffset={_calibration[_currentYaw, 1]:F3}  screenWidth={_calibration[_currentYaw, 2]:F3}  flip={_flipHorizontal[_currentYaw]}");
                SetUIState(UIState.Closed);
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_uiState == UIState.RomList)
                {
                    SetUIState(UIState.Closed);
                }
                return;
            }

            if (_uiState == UIState.RomList)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                // Keep running frames in background while browsing ROMs
                if (_tvOn && _manager.IsRunning)
                    _manager.RunFrame();
                return;
            }

            if (_uiState == UIState.Playing)
            {
                UpdatePlaying();
            }
        }

        private void UpdatePlaying()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _romListScroll = Vector2.zero;
                SetUIState(UIState.RomList);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                _manager.Reset();
                return;
            }

            // --- Calibration controls (numpad) ---
            bool changed = false;
            int y = _currentYaw;

            if (Input.GetKeyDown(KeyCode.Keypad8)) { _calibration[y, 1] += OffsetStep; changed = true; }
            if (Input.GetKeyDown(KeyCode.Keypad2)) { _calibration[y, 1] -= OffsetStep; changed = true; }
            if (Input.GetKeyDown(KeyCode.Keypad6)) { _calibration[y, 0] += OffsetStep; changed = true; }
            if (Input.GetKeyDown(KeyCode.Keypad4)) { _calibration[y, 0] -= OffsetStep; changed = true; }
            if (Input.GetKeyDown(KeyCode.KeypadPlus))  { _calibration[y, 2] += ScaleStep; changed = true; }
            if (Input.GetKeyDown(KeyCode.KeypadMinus)) { _calibration[y, 2] -= ScaleStep; changed = true; }

            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                _flipHorizontal[y] = !_flipHorizontal[y];
                changed = true;
                Log.Out($"[7nes-calibrate] yaw={_currentYaw} flipHorizontal={_flipHorizontal[y]}");
            }

            if (changed)
            {
                UpdateQuadTransform();
                Log.Out($"[7nes-calibrate] yaw={_currentYaw} normalOffset={_calibration[y, 0]:F3}  verticalOffset={_calibration[y, 1]:F3}  screenWidth={_calibration[y, 2]:F3}  flip={_flipHorizontal[y]}");
            }

            // NES controller input
            _manager.SetButton(0, Input.GetKey(KeyCode.A));
            _manager.SetButton(1, Input.GetKey(KeyCode.D));
            _manager.SetButton(2, Input.GetKey(KeyCode.RightShift));
            _manager.SetButton(3, Input.GetKey(KeyCode.Return));
            _manager.SetButton(4, Input.GetKey(KeyCode.UpArrow));
            _manager.SetButton(5, Input.GetKey(KeyCode.DownArrow));
            _manager.SetButton(6, Input.GetKey(KeyCode.LeftArrow));
            _manager.SetButton(7, Input.GetKey(KeyCode.RightArrow));

            _manager.RunFrame();
        }

        // === GUI RENDERING ===
        void InitStyles()
        {
            if (_stylesInitialized) return;

            _panelStyle = new GUIStyle();
            _panelStyle.normal.background = _dimOverlay;

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

        void OnGUI()
        {
            if (_uiState == UIState.Closed) return;

            InitStyles();

            switch (_uiState)
            {
                case UIState.RomList:
                    DrawRomList();
                    break;
                case UIState.Playing:
                    if (_manager.IsRunning)
                    {
                        DrawControlsHint();
                        DrawCalibrationHUD();
                    }
                    break;
            }
        }

        // === ROM LIST ===
        void DrawRomList()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _panelStyle);

            float panelWidth = 500;
            float panelHeight = Screen.height * 0.7f;
            float x = (Screen.width - panelWidth) / 2f;
            float y = (Screen.height - panelHeight) / 2f;

            GUI.Box(new Rect(x - 10, y - 10, panelWidth + 20, panelHeight + 20), "");

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
                            _tvOn = true;
                            EnsureScreenQuad();
                            SetUIState(UIState.Playing);
                        }
                    }
                }

                GUI.EndScrollView();
            }

            // Hint
            var hintStyle2 = new GUIStyle(_labelStyle);
            hintStyle2.fontSize = 12;
            hintStyle2.normal.textColor = new Color(1, 1, 1, 0.5f);
            GUI.Label(new Rect(x, y + panelHeight - 35, panelWidth, 25), "Press Esc to close", hintStyle2);
        }

        // === PLAYING HUD ===
        void DrawControlsHint()
        {
            var hintStyle = new GUIStyle(_labelStyle);
            hintStyle.fontSize = 12;
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);

            float hintHeight = 25;
            float hintY = Screen.height - hintHeight - 10;

            GUI.Box(new Rect(0, hintY - 5, Screen.width, hintHeight + 10), "", _panelStyle);
            GUI.Label(new Rect(0, hintY, Screen.width, hintHeight),
                "Arrows=D-Pad | A=A | D=B | Enter=Start | RShift=Select | Tab=ROM List | F5=Reset | Esc=Close",
                hintStyle);
        }

        void DrawCalibrationHUD()
        {
            var hudStyle = new GUIStyle(GUI.skin.label);
            hudStyle.fontSize = 14;
            hudStyle.normal.textColor = Color.yellow;

            float x = 10, y = 10, lineH = 20;

            string[] yawNames = { "North", "East", "South", "West" };
            GUI.Label(new Rect(x, y, 400, lineH), $"--- SCREEN CALIBRATION (Rotation {_currentYaw}: {yawNames[_currentYaw]}) ---", hudStyle);
            y += lineH;
            GUI.Label(new Rect(x, y, 400, lineH), $"Normal offset (in/out): {_calibration[_currentYaw, 0]:F3}  [Numpad 4/6]", hudStyle);
            y += lineH;
            GUI.Label(new Rect(x, y, 400, lineH), $"Vertical offset (up/dn): {_calibration[_currentYaw, 1]:F3}  [Numpad 8/2]", hudStyle);
            y += lineH;
            GUI.Label(new Rect(x, y, 400, lineH), $"Screen width (size):     {_calibration[_currentYaw, 2]:F3}  [Numpad +/-]", hudStyle);
            y += lineH;
            GUI.Label(new Rect(x, y, 400, lineH), $"Flip horizontal:         {_flipHorizontal[_currentYaw]}      [Numpad 0]", hudStyle);
            y += lineH;
            GUI.Label(new Rect(x, y, 400, lineH), "Step: 0.01 (offset) / 0.02 (size)", hudStyle);
        }

        // === CLEANUP ===
        void OnDestroy()
        {
            DestroyScreenQuad();
        }
    }
}
