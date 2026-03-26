using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SevenNes.Integration
{
    public class NesEmulatorWindow : MonoBehaviour
    {
        private static NesEmulatorWindow _instance;

        // === STATE ===
        private enum UIState { Closed, Playing, Controls }
        private UIState _uiState = UIState.Closed;

        // Hand hiding while using TV
        private bool _handsHidden;
        private List<SkinnedMeshRenderer> _hiddenHandRenderers = new List<SkinnedMeshRenderer>();
        private List<MeshRenderer> _hiddenItemRenderers = new List<MeshRenderer>();
        private bool _tvOn; // TV is powered on (screen quad visible, emulator runs in background)

        private NesEmulatorManager _manager;
        private NesInputBindings _bindings;

        // GUI styles
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _panelStyle;
        private bool _stylesInitialized;

        // In-world screen rendering
        private GameObject _screenQuad;
        private Material _screenMaterial;
        private Texture2D _dimOverlay;
        private Texture2D _noSignalTexture;

        // Cartridge monitoring
        private float _cartridgeCheckTimer;
        private const float CartridgeCheckInterval = 0.25f;
        private string _lastCartridgeName;

        // Per-rotation calibration: [normalOffset, verticalOffset, screenWidth] for each yaw (0-3)
        private static readonly float[,] DefaultCalibration = new float[,]
        {
            { 0.250f, 0.080f, 0.870f }, // Rotation 0 (North)
            { 0.250f, 0.080f, 0.870f }, // Rotation 1 (East)
            { 0.250f, 0.080f, 0.870f }, // Rotation 2 (South)
            { 0.250f, 0.080f, 0.870f }, // Rotation 3 (West)
        };

        // Per-rotation horizontal flip (true = mirror the texture)
        private static readonly bool[] DefaultFlip = { false, true, false, true };

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

        // Fullscreen overlay mode
        private bool _isFullscreen;
        private bool _fullscreenKeyHeld;

        // Notification (brief on-screen message)
        private string _notification;
        private float _notificationTimer;


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

            LoadNoSignalTexture();
        }

        private void LoadNoSignalTexture()
        {
            try
            {
                string path = Path.Combine(ModInit.ModPath, "Resources", "nosignal.jpg");
                if (File.Exists(path))
                {
                    byte[] data = File.ReadAllBytes(path);
                    _noSignalTexture = new Texture2D(2, 2);
                    _noSignalTexture.LoadImage(data);
                    _noSignalTexture.Apply();
                    Log.Out("[7nes] Loaded no-signal image");
                }
                else
                {
                    Log.Warning("[7nes] No-signal image not found: " + path);
                    CreateFallbackNoSignalTexture();
                }
            }
            catch (System.Exception e)
            {
                Log.Error("[7nes] Failed to load no-signal image: " + e.Message);
                CreateFallbackNoSignalTexture();
            }
        }

        private void CreateFallbackNoSignalTexture()
        {
            _noSignalTexture = new Texture2D(256, 240);
            var pixels = new Color32[256 * 240];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(20, 20, 20, 255);
            _noSignalTexture.SetPixels32(pixels);
            _noSignalTexture.Apply();
        }

        // === PUBLIC PROPERTIES (used by Harmony patches for dynamic command state) ===
        public bool IsTvOn => _tvOn;
        public bool HasLoadedRom => _manager != null && _manager.HasLoadedRom;

        // === PUBLIC API (called from Harmony patches) ===

        /// Auto-show screen quad when TV is powered (called from GetActivationText patch)
        public void AutoShowScreen(Vector3i blockPos, byte rotation, bool isPowered)
        {
            if (_uiState == UIState.Playing || _uiState == UIState.Controls)
                return;

            if (!isPowered)
            {
                if (_tvOn && _blockPos.Equals(blockPos))
                {
                    _tvOn = false;
                    _lastCartridgeName = null;
                    _manager.Stop();
                    DestroyScreenQuad();
                }
                return;
            }

            // Already showing for this block
            if (_tvOn && _screenQuad != null && _blockPos.Equals(blockPos))
                return;

            SetBlockInfo(blockPos, rotation);

            // Check for cartridge
            var world = GameManager.Instance?.World;
            if (world != null)
            {
                string cartridge = CartridgeHelper.FindNearbyCartridge(world, 0, blockPos);
                _lastCartridgeName = cartridge;
                if (cartridge != null)
                    _manager.LoadRomByItemName(cartridge);
            }

            if (_manager.HasLoadedRom)
                _manager.Resume();

            _tvOn = true;
            EnsureScreenQuad();
            UpdateScreenMaterial();
        }

        /// Default E press: find cartridge in nearby nesConsole and play
        public void HandleActivate(Vector3i blockPos, byte rotation)
        {
            if (_uiState == UIState.Playing) return;
            if (_closeCooldown > 0f) return;
            SetBlockInfo(blockPos, rotation);

            // Try to load from nearby nesConsole cartridge
            var world = GameManager.Instance?.World;
            if (world != null)
            {
                string cartridge = CartridgeHelper.FindNearbyCartridge(world, 0, blockPos);
                _lastCartridgeName = cartridge;
                if (cartridge != null)
                {
                    if (_manager.LoadRomByItemName(cartridge))
                    {
                        if (!_manager.IsRunning)
                            _manager.Resume();
                        _tvOn = true;
                        EnsureScreenQuad();
                        UpdateScreenMaterial();
                        SetUIState(UIState.Playing);
                        return;
                    }
                    else if (_manager.LastLoadError != null)
                    {
                        ShowNotification(_manager.LastLoadError, 5f);
                    }
                }
            }

            // No cartridge found — resume if already loaded, otherwise do nothing
            if (_manager.HasLoadedRom)
            {
                if (!_manager.IsRunning)
                    _manager.Resume();
                _tvOn = true;
                EnsureScreenQuad();
                UpdateScreenMaterial();
                SetUIState(UIState.Playing);
            }
        }

        /// Hold-E radial: Turn On — find cartridge and run in background
        public void HandleTurnOn(Vector3i blockPos, byte rotation)
        {
            if (_uiState == UIState.Playing) return;
            SetBlockInfo(blockPos, rotation);

            // Try to load from nearby nesConsole cartridge
            var world = GameManager.Instance?.World;
            if (world != null)
            {
                string cartridge = CartridgeHelper.FindNearbyCartridge(world, 0, blockPos);
                _lastCartridgeName = cartridge;
                if (cartridge != null)
                    _manager.LoadRomByItemName(cartridge);
            }

            if (_manager.HasLoadedRom)
                _manager.Resume();

            _tvOn = true;
            EnsureScreenQuad();
            UpdateScreenMaterial();
            ClearControllerInput();
            Log.Out("[7nes] TV turned on");
            SetUIState(UIState.Closed);
        }

        /// Hold-E radial: Turn Off
        public void HandleTurnOff(Vector3i blockPos, byte rotation)
        {
            if (_uiState == UIState.Playing) return;
            SetBlockInfo(blockPos, rotation);

            _tvOn = false;
            _lastCartridgeName = null;
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
            ClearControllerInput();
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

        private void TryQuickInsertCartridge()
        {
            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null) return;

            // Check if player is holding a cartridge
            var holding = player.inventory?.holdingItemItemValue;
            if (holding == null || holding.IsEmpty() || holding.ItemClass == null) return;
            if (!holding.ItemClass.GetItemName().StartsWith("nesCart_")) return;

            // Raycast from player camera to find what block they're looking at
            var cam = player.cameraTransform;
            if (cam == null) return;

            if (!Physics.Raycast(new Ray(cam.position, cam.forward), out RaycastHit hit, 3.5f))
                return;

            // Get the block position from the hit point
            // Step slightly into the block from the hit face to get the correct voxel
            Vector3 probePoint = hit.point + cam.forward * 0.05f;
            var blockPos = new Vector3i(
                Mathf.FloorToInt(probePoint.x),
                Mathf.FloorToInt(probePoint.y),
                Mathf.FloorToInt(probePoint.z));

            var world = GameManager.Instance.World;
            var bv = world.GetBlock(blockPos);
            if (bv.Block == null || bv.Block.GetBlockName() != "nesConsole") return;

            var te = world.GetTileEntity(0, blockPos) as TileEntityLootContainer;
            if (te == null) return;

            // Get what's currently in the slot
            var oldSlot = (te.items != null && te.items.Length > 0) ? te.items[0] : ItemStack.Empty;

            // Insert the held cartridge
            te.UpdateSlot(0, new ItemStack(holding.Clone(), 1));

            // Remove one from the player's hand
            var holdingStack = player.inventory.holdingItemStack;
            if (holdingStack.count > 1)
            {
                holdingStack.count--;
                player.inventory.SetItem(player.inventory.holdingItemIdx, holdingStack);
            }
            else
            {
                player.inventory.SetItem(player.inventory.holdingItemIdx, ItemStack.Empty);
            }

            // Return old cartridge to player if there was one
            if (!oldSlot.IsEmpty())
            {
                if (!player.inventory.AddItem(oldSlot))
                {
                    GameManager.Instance.ItemDropServer(
                        new ItemStack(oldSlot.itemValue, oldSlot.count),
                        player.GetPosition(), Vector3.zero);
                }
            }

            te.SetModified();
            Log.Out($"[7nes] Quick-inserted cartridge: {holding.ItemClass.GetItemName()}");
        }

        private void CheckCartridgeState()
        {
            var world = GameManager.Instance?.World;
            if (world == null) return;

            string currentCartridge = CartridgeHelper.FindNearbyCartridge(world, 0, _blockPos);

            // Cartridge was removed
            if (_lastCartridgeName != null && currentCartridge == null)
            {
                Log.Out("[7nes] Cartridge removed — unloading game");
                _isFullscreen = false;
                _manager.UnloadRom();
                _lastCartridgeName = null;
                UpdateScreenMaterial();

                if (_uiState == UIState.Playing)
                {
                    ClearControllerInput();
                    _closeCooldown = CloseCooldownDuration;
                    SetUIState(UIState.Closed);
                }
                return;
            }

            // Cartridge was inserted or swapped — auto-load
            if (currentCartridge != null && currentCartridge != _lastCartridgeName)
            {
                Log.Out($"[7nes] Cartridge detected — auto-loading: {currentCartridge}");
                _lastCartridgeName = currentCartridge;
                if (_manager.LoadRomByItemName(currentCartridge))
                {
                    if (!_manager.IsRunning)
                        _manager.Resume();
                    UpdateScreenMaterial();
                }
                return;
            }

            _lastCartridgeName = currentCartridge;
        }

        private void UpdateScreenMaterial()
        {
            if (_screenMaterial == null) return;
            _screenMaterial.mainTexture = _manager.HasLoadedRom ? _manager.ScreenTexture : _noSignalTexture;
            UpdateQuadTransform();
        }

        // === STATE MANAGEMENT ===
        private void SetUIState(UIState newState)
        {
            _uiState = newState;
            switch (newState)
            {
                case UIState.Closed:
                    UnlockPlayer();
                    ShowPlayerHands();
                    SetHUDVisible(true);
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    break;
                case UIState.Controls:
                    LockPlayer();
                    HidePlayerHands();
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    break;
                case UIState.Playing:
                    LockPlayer();
                    HidePlayerHands();
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

        private void HidePlayerHands()
        {
            if (_handsHidden) return;
            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null) return;

            // Hide arms/body (SkinnedMeshRenderers)
            var skinnedRenderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            foreach (var smr in skinnedRenderers)
            {
                if (smr.enabled)
                {
                    smr.enabled = false;
                    _hiddenHandRenderers.Add(smr);
                }
            }

            // Hide held items like guns, tools, etc. (regular MeshRenderers)
            var meshRenderers = player.GetComponentsInChildren<MeshRenderer>(false);
            foreach (var mr in meshRenderers)
            {
                if (mr.enabled)
                {
                    mr.enabled = false;
                    _hiddenItemRenderers.Add(mr);
                }
            }

            if (_hiddenHandRenderers.Count > 0 || _hiddenItemRenderers.Count > 0)
                _handsHidden = true;
        }

        private void ShowPlayerHands()
        {
            if (!_handsHidden) return;
            foreach (var smr in _hiddenHandRenderers)
            {
                if (smr != null)
                    smr.enabled = true;
            }
            _hiddenHandRenderers.Clear();
            foreach (var mr in _hiddenItemRenderers)
            {
                if (mr != null)
                    mr.enabled = true;
            }
            _hiddenItemRenderers.Clear();
            _handsHidden = false;
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
            _screenMaterial.mainTexture = _manager.HasLoadedRom ? _manager.ScreenTexture : _noSignalTexture;
            _screenQuad.GetComponent<Renderer>().material = _screenMaterial;

            _screenQuad.transform.rotation = Quaternion.LookRotation(-_screenNormal, Vector3.up);
            UpdateQuadTransform();
        }

        // No-signal screen calibration (adjustable at runtime via numpad)
        private const float _noSignalNormalOffset = 0.250f;
        private const float _noSignalVerticalOffset = 0.060f;
        private const float _noSignalWidth = 1.540f;

        private void UpdateQuadTransform()
        {
            if (_screenQuad == null) return;

            bool showingNoSignal = !_manager.HasLoadedRom;

            float normalOffset = showingNoSignal ? _noSignalNormalOffset : _calibration[_currentYaw, 0];
            float verticalOffset = showingNoSignal ? _noSignalVerticalOffset : _calibration[_currentYaw, 1];
            float screenWidth = showingNoSignal ? _noSignalWidth : _calibration[_currentYaw, 2];

            Vector3 screenCenter = _blockCenter + _screenNormal * normalOffset + Vector3.up * verticalOffset;
            _screenQuad.transform.position = screenCenter;

            // Use the displayed texture's native aspect ratio
            float aspectRatio = 240f / 256f;
            if (_screenMaterial != null && _screenMaterial.mainTexture != null)
            {
                var tex = _screenMaterial.mainTexture;
                if (tex.width > 0 && tex.height > 0)
                    aspectRatio = (float)tex.height / tex.width;
            }
            float screenHeight = screenWidth * aspectRatio;
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

            // Periodically check if power was lost or cartridge removed while TV is on
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
                        _lastCartridgeName = null;
                        _manager.Stop();
                        ClearControllerInput();
                        DestroyScreenQuad();
                        if (_uiState != UIState.Closed)
                            SetUIState(UIState.Closed);
                        return;
                    }
                }

                _cartridgeCheckTimer += Time.deltaTime;
                if (_cartridgeCheckTimer >= CartridgeCheckInterval)
                {
                    _cartridgeCheckTimer = 0f;
                    CheckCartridgeState();
                }
            }

            // Quick-insert: left-click while holding a cartridge and looking at a nesConsole
            if (Input.GetMouseButtonDown(0) && _uiState == UIState.Closed)
            {
                TryQuickInsertCartridge();
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
                // Prevent any key presses from reaching the game while controls dialog is open
                Input.ResetInputAxes();
                return;
            }

            // E closes play mode
            if (_uiState == UIState.Playing && Input.GetKeyDown(KeyCode.E))
            {
                _isFullscreen = false;
                ClearControllerInput();
                _tvOn = true;
                _closeCooldown = CloseCooldownDuration;
                SetUIState(UIState.Closed);
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
                if (_rebindIndex == NesInputBindings.ButtonCount)
                {
                    // Fullscreen extra binding
                    _bindings.FullscreenKey = detected;
                }
                else if (_rebindIsGamepad)
                    _bindings.SetGamepad(_rebindIndex, detected);
                else
                    _bindings.SetKeyboard(_rebindIndex, detected);
                _rebindIndex = -1;
            }
        }

        private void UpdatePlaying()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                _manager.Reset();
                return;
            }

            // Fullscreen toggle — track key edge in Update
            if (_bindings.FullscreenKey != KeyCode.None)
            {
                bool pressed = Input.GetKey(_bindings.FullscreenKey);
                if (pressed && !_fullscreenKeyHeld)
                {
                    _isFullscreen = !_isFullscreen;
                    Log.Out($"[7nes] Fullscreen={_isFullscreen} (key={_bindings.FullscreenKey})");
                }
                _fullscreenKeyHeld = pressed;
            }
            // Debug: log once if GetKeyDown sees U at all
            if (Input.GetKeyDown(KeyCode.U))
                Log.Out("[7nes] DEBUG: Input.GetKeyDown(U) fired");
            if (Input.GetKeyDown(KeyCode.Tab))
                Log.Out("[7nes] DEBUG: Input.GetKeyDown(Tab) fired");

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

            _stylesInitialized = true;
        }

        void OnGUI()
        {
            // Draw notification even when Closed
            if (_notificationTimer > 0f)
            {
                InitStyles();
                DrawNotification();
            }

            if (_uiState == UIState.Closed) return;

            InitStyles();

            switch (_uiState)
            {
                case UIState.Playing:
                    if (_manager.IsRunning)
                    {
                        if (_isFullscreen)
                            DrawFullscreen();
                        DrawControlsHint();
                    }
                    break;
                case UIState.Controls:
                    DrawControlsDialog();
                    // Consume all keyboard/gamepad events so they don't reach the game
                    if (Event.current != null &&
                        (Event.current.isKey || Event.current.isMouse ||
                         Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp))
                    {
                        Event.current.Use();
                    }
                    break;
            }
        }

        // === NOTIFICATION ===
        private void ShowNotification(string message, float duration = 3f)
        {
            _notification = message;
            _notificationTimer = duration;
        }

        void DrawNotification()
        {
            _notificationTimer -= Time.deltaTime;
            if (_notificationTimer <= 0f) return;

            float alpha = Mathf.Min(1f, _notificationTimer);
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = 18;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color(1f, 1f, 1f, alpha);
            style.wordWrap = true;

            float w = 500;
            float h = 60;
            float x = (Screen.width - w) / 2f;
            float y = Screen.height * 0.3f;

            var bgColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f * alpha);
            GUI.Box(new Rect(x - 10, y - 10, w + 20, h + 20), "");
            GUI.color = bgColor;

            GUI.Label(new Rect(x, y, w, h), _notification, style);
        }

        // === FULLSCREEN OVERLAY ===
        void DrawFullscreen()
        {
            var tex = _manager.ScreenTexture;
            if (tex == null) return;

            float screenW = Screen.width;
            float screenH = Screen.height;

            // Black letterbox background
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Texture2D.blackTexture);

            // Maintain NES aspect ratio (256:240 = 16:15)
            float nesAspect = 256f / 240f;
            float drawH = screenH;
            float drawW = drawH * nesAspect;
            if (drawW > screenW)
            {
                drawW = screenW;
                drawH = drawW / nesAspect;
            }

            float x = (screenW - drawW) / 2f;
            float y = (screenH - drawH) / 2f;

            // Draw NES screen — use simple GUI.DrawTexture for reliability.
            // Colors may be slightly off vs the in-world quad (gamma difference)
            // but this guarantees visibility in all 7DTD rendering configurations.
            GUI.DrawTexture(new Rect(x, y, drawW, drawH), tex, ScaleMode.StretchToFill);
        }

        // === CONTROLS DIALOG ===
        void DrawControlsDialog()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _panelStyle);

            float panelWidth = 520;
            float panelHeight = 520;
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

            // Extra bindings separator
            float extraY = rowStart + NesInputBindings.ButtonCount * rowH + 5;
            var sepStyle = new GUIStyle(_labelStyle);
            sepStyle.fontSize = 13;
            sepStyle.fontStyle = FontStyle.Bold;
            sepStyle.normal.textColor = new Color(0.6f, 0.8f, 1f);
            GUI.Label(new Rect(col0, extraY, panelWidth - 40, 25), "--- System ---", sepStyle);

            float extraRowY = extraY + 28;

            // Fullscreen binding (rebind index = ButtonCount i.e. 8)
            GUI.Label(new Rect(col0, extraRowY, 110, 32), "Fullscreen", nameStyle);
            bool isRebindingFs = (_rebindIndex == NesInputBindings.ButtonCount && !_rebindIsGamepad);
            string fsText = isRebindingFs ? "< Press Key >" : NesInputBindings.KeyName(_bindings.FullscreenKey);
            GUIStyle fsStyle = isRebindingFs ? listeningBtnStyle : bindBtnStyle;
            if (GUI.Button(new Rect(col1, extraRowY, colW1, 32), fsText, fsStyle))
            {
                if (!isRebindingFs)
                {
                    _rebindIndex = NesInputBindings.ButtonCount;
                    _rebindIsGamepad = false;
                }
            }

            // Bottom buttons
            float btnY = extraRowY + rowH + 15;
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
                    if (_rebindIndex == NesInputBindings.ButtonCount)
                        _bindings.FullscreenKey = KeyCode.None;
                    else if (_rebindIsGamepad)
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
            hintStyle.fontSize = 18;
            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);

            float hintHeight = 30;
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
