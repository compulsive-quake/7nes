using System.IO;
using UnityEngine;

namespace SevenNes.Integration
{
    public class NesEmulatorWindow : MonoBehaviour
    {
        private static NesEmulatorWindow _instance;

        // === STATE ===
        private enum UIState { Closed, RomList, Playing, Controls }
        private UIState _uiState = UIState.Closed;
        private bool _tvOn; // TV is powered on (screen quad visible, emulator runs in background)

        private NesEmulatorManager _manager;
        private NesInputBindings _bindings;
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

        // Cooldown to prevent E-close from immediately re-triggering block interaction
        private float _closeCooldown;
        private const float CloseCooldownDuration = 0.5f;

        // Controls rebinding state
        private int _rebindIndex = -1;       // which button (0-7) is being rebound, -1 = none
        private bool _rebindIsGamepad;        // true = rebinding gamepad column, false = keyboard
        private Vector2 _controlsScroll;


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
            _bindings = NesInputBindings.Instance;
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
            if (_closeCooldown > 0f) return;
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

        /// Hold-E radial: Controls
        public void HandleControls(Vector3i blockPos, byte rotation)
        {
            if (_uiState == UIState.Playing) return;
            SetBlockInfo(blockPos, rotation);
            _rebindIndex = -1;
            _controlsScroll = Vector2.zero;
            SetUIState(UIState.Controls);
        }

        private void SetBlockInfo(Vector3i blockPos, byte rotation)
        {
            _blockPos = blockPos;
            _blockRotation = rotation;
            _currentYaw = rotation & 0x3;
            _blockCenter = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
            _screenNormal = GetScreenNormal(rotation);
        }

        private bool CheckPowerState()
        {
            var world = GameManager.Instance?.World;
            if (world == null) return false;
            return PowerHelper.IsPowered(world, 0, _blockPos);
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
                case UIState.Controls:
                    LockPlayer();
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
        private float _powerCheckTimer;
        private const float PowerCheckInterval = 0.5f;

        void Update()
        {
            // Tick down close cooldown
            if (_closeCooldown > 0f)
                _closeCooldown -= Time.deltaTime;

            // Periodically check if power was lost while TV is on
            if (_tvOn)
            {
                _powerCheckTimer += Time.deltaTime;
                if (_powerCheckTimer >= PowerCheckInterval)
                {
                    _powerCheckTimer = 0f;
                    if (!CheckPowerState())
                    {
                        Log.Out("[7nes] Power lost — turning TV off");
                        _tvOn = false;
                        _manager.Stop();
                        ClearControllerInput();
                        DestroyScreenQuad();
                        if (_uiState != UIState.Closed)
                            SetUIState(UIState.Closed);
                        return;
                    }
                }
            }

            // Background mode: keep running emulator frames even when UI is closed
            if (_tvOn && _manager.IsRunning && _uiState == UIState.Closed)
            {
                _manager.RunFrame();
                return;
            }

            if (_uiState == UIState.Closed) return;

            // Controls rebinding: detect key/button presses
            if (_uiState == UIState.Controls)
            {
                UpdateControlsRebind();
                // Keep running frames in background
                if (_tvOn && _manager.IsRunning)
                    _manager.RunFrame();
                return;
            }

            // E closes play mode, Escape closes ROM list
            if (_uiState == UIState.Playing && Input.GetKeyDown(KeyCode.E))
            {
                // Exit play mode — TV stays on in background
                ClearControllerInput();
                _tvOn = true;
                _closeCooldown = CloseCooldownDuration;
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

        private void UpdateControlsRebind()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (_rebindIndex < 0) return;

            // Cancel rebind with Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _rebindIndex = -1;
                return;
            }

            KeyCode detected = NesInputBindings.DetectKeyDown(_rebindIsGamepad);
            if (detected != KeyCode.None)
            {
                if (_rebindIsGamepad)
                    _bindings.SetGamepad(_rebindIndex, detected);
                else
                    _bindings.SetKeyboard(_rebindIndex, detected);
                _rebindIndex = -1;
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

            // NES controller input — use configurable bindings
            for (int i = 0; i < NesInputBindings.ButtonCount; i++)
                _manager.SetButton(i, _bindings.IsPressed(i));

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
                    }
                    break;
                case UIState.Controls:
                    DrawControlsDialog();
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

        // === CONTROLS DIALOG ===
        void DrawControlsDialog()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _panelStyle);

            float panelWidth = 520;
            float panelHeight = 460;
            float px = (Screen.width - panelWidth) / 2f;
            float py = (Screen.height - panelHeight) / 2f;

            GUI.Box(new Rect(px - 10, py - 10, panelWidth + 20, panelHeight + 20), "");

            // Title
            var titleStyle = new GUIStyle(_labelStyle);
            titleStyle.fontSize = 24;
            titleStyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(px, py, panelWidth, 40), "NES Controls", titleStyle);

            float headerY = py + 50;

            // Column headers
            var headerStyle = new GUIStyle(_labelStyle);
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

            float col0 = px + 20;
            float col1 = px + 140;
            float col2 = px + 320;
            float colW1 = 150;
            float colW2 = 150;
            float rowH = 38;

            GUI.Label(new Rect(col0, headerY, 100, 30), "NES Button", headerStyle);
            GUI.Label(new Rect(col1, headerY, colW1, 30), "Keyboard", headerStyle);
            GUI.Label(new Rect(col2, headerY, colW2, 30), "Controller", headerStyle);

            float rowStart = headerY + 35;

            // Binding rows
            var nameStyle = new GUIStyle(_labelStyle);
            nameStyle.fontSize = 15;
            nameStyle.alignment = TextAnchor.MiddleLeft;
            nameStyle.normal.textColor = Color.white;

            var bindBtnStyle = new GUIStyle(GUI.skin.button);
            bindBtnStyle.fontSize = 14;
            bindBtnStyle.alignment = TextAnchor.MiddleCenter;

            var listeningBtnStyle = new GUIStyle(bindBtnStyle);
            listeningBtnStyle.normal.textColor = Color.yellow;
            listeningBtnStyle.fontStyle = FontStyle.Bold;

            for (int i = 0; i < NesInputBindings.ButtonCount; i++)
            {
                float ry = rowStart + i * rowH;

                // Button name
                GUI.Label(new Rect(col0, ry, 110, 32), NesInputBindings.ButtonNames[i], nameStyle);

                // Keyboard binding
                bool isRebindingKb = (_rebindIndex == i && !_rebindIsGamepad);
                string kbText = isRebindingKb ? "< Press Key >" : NesInputBindings.KeyName(_bindings.GetKeyboard(i));
                GUIStyle kbStyle = isRebindingKb ? listeningBtnStyle : bindBtnStyle;
                if (GUI.Button(new Rect(col1, ry, colW1, 32), kbText, kbStyle))
                {
                    if (!isRebindingKb)
                    {
                        _rebindIndex = i;
                        _rebindIsGamepad = false;
                    }
                }

                // Gamepad binding
                bool isRebindingGp = (_rebindIndex == i && _rebindIsGamepad);
                string gpText = isRebindingGp ? "< Press Button >" : NesInputBindings.KeyName(_bindings.GetGamepad(i));
                GUIStyle gpStyle = isRebindingGp ? listeningBtnStyle : bindBtnStyle;
                if (GUI.Button(new Rect(col2, ry, colW2, 32), gpText, gpStyle))
                {
                    if (!isRebindingGp)
                    {
                        _rebindIndex = i;
                        _rebindIsGamepad = true;
                    }
                }
            }

            // Bottom buttons
            float btnY = rowStart + NesInputBindings.ButtonCount * rowH + 15;
            float btnW = 140;
            float btnH = 35;
            float btnSpacing = 20;
            float totalBtnW = btnW * 3 + btnSpacing * 2;
            float btnStartX = px + (panelWidth - totalBtnW) / 2f;

            if (GUI.Button(new Rect(btnStartX, btnY, btnW, btnH), "Reset Defaults", _buttonStyle))
            {
                _bindings.ResetDefaults();
                _rebindIndex = -1;
            }

            if (GUI.Button(new Rect(btnStartX + btnW + btnSpacing, btnY, btnW, btnH), "Clear", _buttonStyle))
            {
                if (_rebindIndex >= 0)
                {
                    if (_rebindIsGamepad)
                        _bindings.SetGamepad(_rebindIndex, KeyCode.None);
                    else
                        _bindings.SetKeyboard(_rebindIndex, KeyCode.None);
                    _rebindIndex = -1;
                }
            }

            if (GUI.Button(new Rect(btnStartX + (btnW + btnSpacing) * 2, btnY, btnW, btnH), "Save & Close", _buttonStyle))
            {
                _bindings.Save();
                _rebindIndex = -1;
                SetUIState(UIState.Closed);
            }

            // Hint
            var hintStyle = new GUIStyle(_labelStyle);
            hintStyle.fontSize = 12;
            hintStyle.normal.textColor = new Color(1, 1, 1, 0.4f);
            string hintText = _rebindIndex >= 0 ? "Press Esc to cancel rebinding" : "Click a binding to change it";
            GUI.Label(new Rect(px, btnY + btnH + 10, panelWidth, 25), hintText, hintStyle);
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
                _bindings.GetControlsHintString(),
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
