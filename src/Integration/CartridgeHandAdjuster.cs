using UnityEngine;

namespace SevenNes.Integration
{
    public class CartridgeHandAdjuster : MonoBehaviour
    {
        private static CartridgeHandAdjuster _instance;
        public static CartridgeHandAdjuster Instance => _instance;

        private bool _showDebugUI = false; // Toggle with Numpad * to fine-tune
        private Transform _cartridgeTransform;
        private string _statusText = "Searching...";

        private enum AdjustMode { Position, Rotation, Scale }
        private AdjustMode _mode = AdjustMode.Position;

        // Absolute transform values (override whatever the game sets)
        private Vector3 _position = new Vector3(-0.025f, -0.005f, 0f);
        private Vector3 _rotation = new Vector3(0f, -40f, -5f);
        private float _scale = 0.017f;

        // Step sizes
        private float _posStep = 0.005f;
        private float _rotStep = 5f;
        private float _scaleStep = 0.001f;

        // GUI
        private Texture2D _bgTex;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _modeStyle;

        // Search throttle
        private float _nextSearchTime;

        // Material fix tracking
        private bool _materialFixed;

        // Track held item to detect switches
        private int _lastHeldItemIdx = -1;
        private string _lastHeldItemName;

        void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (!IsInGame()) return;

            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
                _showDebugUI = !_showDebugUI;

            if (!_showDebugUI) return;

            if (Input.GetKeyDown(KeyCode.KeypadDivide))
                _mode = (AdjustMode)(((int)_mode + 1) % 3);

            if (Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                switch (_mode)
                {
                    case AdjustMode.Position: _posStep *= 2f; break;
                    case AdjustMode.Rotation: _rotStep *= 2f; break;
                    case AdjustMode.Scale: _scaleStep *= 2f; break;
                }
            }
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                switch (_mode)
                {
                    case AdjustMode.Position: _posStep = Mathf.Max(0.001f, _posStep / 2f); break;
                    case AdjustMode.Rotation: _rotStep = Mathf.Max(0.5f, _rotStep / 2f); break;
                    case AdjustMode.Scale: _scaleStep = Mathf.Max(0.0005f, _scaleStep / 2f); break;
                }
            }

            if (Input.GetKeyDown(KeyCode.Keypad6)) Adjust(0, 1f);
            if (Input.GetKeyDown(KeyCode.Keypad4)) Adjust(0, -1f);
            if (Input.GetKeyDown(KeyCode.Keypad8)) Adjust(1, 1f);
            if (Input.GetKeyDown(KeyCode.Keypad2)) Adjust(1, -1f);
            if (Input.GetKeyDown(KeyCode.Keypad9)) Adjust(2, 1f);
            if (Input.GetKeyDown(KeyCode.Keypad7)) Adjust(2, -1f);
        }

        void LateUpdate()
        {
            if (!IsInGame()) return;

            CheckHeldItemChanged();
            FindCartridgeTransform();

            if (_cartridgeTransform == null) return;

            // Force absolute values — ignore whatever the game set
            _cartridgeTransform.localPosition = _position;
            _cartridgeTransform.localEulerAngles = _rotation;
            _cartridgeTransform.localScale = new Vector3(_scale, _scale, _scale);
        }

        private void CheckHeldItemChanged()
        {
            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null) return;

            var inv = player.inventory;
            int idx = inv.holdingItemIdx;
            string itemName = inv.holdingItem?.Name;

            if (idx != _lastHeldItemIdx || itemName != _lastHeldItemName)
            {
                _lastHeldItemIdx = idx;
                _lastHeldItemName = itemName;
                // Held item changed — clear cached transform so we re-search immediately
                _cartridgeTransform = null;
                _materialFixed = false;
                _nextSearchTime = 0f;
            }
        }

        private void Adjust(int axis, float direction)
        {
            switch (_mode)
            {
                case AdjustMode.Position:
                    _position[axis] += direction * _posStep;
                    break;
                case AdjustMode.Rotation:
                    _rotation[axis] += direction * _rotStep;
                    break;
                case AdjustMode.Scale:
                    _scale = Mathf.Max(0.001f, _scale + direction * _scaleStep);
                    break;
            }
        }

        private void FindCartridgeTransform()
        {
            // Verify cached transform is still valid and active
            if (_cartridgeTransform != null)
            {
                if (_cartridgeTransform.gameObject.activeInHierarchy)
                    return;
                // Cached transform became inactive — clear and re-search
                _cartridgeTransform = null;
                _materialFixed = false;
            }

            if (Time.unscaledTime < _nextSearchTime) return;
            _nextSearchTime = Time.unscaledTime + 0.5f;

            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player != null)
            {
                // Search for exact name or name with (Clone) suffix
                var found = FindInChildrenStartsWith(player.transform, "NESCartridgePrefab");
                if (found != null)
                {
                    _cartridgeTransform = found;
                    _materialFixed = false;
                    _statusText = "Found (player): " + GetTransformPath(found);
                    FixCartridgeMaterial(found);
                    return;
                }

                // Fallback: partial match for any cartridge-related name
                found = FindInChildrenPartial(player.transform, "nescartridge", "NESCartridge", "nesCart");
                if (found != null)
                {
                    _cartridgeTransform = found;
                    _materialFixed = false;
                    _statusText = "Found (partial): " + GetTransformPath(found);
                    FixCartridgeMaterial(found);
                    return;
                }
            }

            _statusText = player != null ? "Cartridge not found - hold a cartridge item" : "No player found";
        }

        private void FixCartridgeMaterial(Transform cartridge)
        {
            if (_materialFixed) return;
            _materialFixed = true;
            CartridgeMaterialFixer.FixMaterials(cartridge);
        }

        private Transform FindInChildrenStartsWith(Transform parent, string prefix)
        {
            if (parent.name.StartsWith(prefix) && parent.gameObject.activeInHierarchy) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindInChildrenStartsWith(parent.GetChild(i), prefix);
                if (result != null) return result;
            }
            return null;
        }

        private Transform FindInChildrenPartial(Transform parent, params string[] patterns)
        {
            string lowerName = parent.name.ToLowerInvariant();
            foreach (var pat in patterns)
            {
                if (lowerName.Contains(pat.ToLowerInvariant()))
                    return parent;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindInChildrenPartial(parent.GetChild(i), patterns);
                if (result != null) return result;
            }
            return null;
        }

        private string GetTransformPath(Transform t)
        {
            string path = t.name;
            Transform cur = t.parent;
            int depth = 0;
            while (cur != null && depth < 6)
            {
                path = cur.name + "/" + path;
                cur = cur.parent;
                depth++;
            }
            if (cur != null) path = ".../" + path;
            return path;
        }

        private bool IsInGame()
        {
            return GameManager.Instance != null
                && GameManager.Instance.World != null
                && GameManager.Instance.World.GetPrimaryPlayer() != null;
        }

        void OnGUI()
        {
            if (!_showDebugUI) return;
            if (!IsInGame()) return;

            EnsureStyles();

            float w = 540;
            float h = 320;
            float x = Screen.width - w - 10;
            float y = 300;

            GUI.DrawTexture(new Rect(x, y, w, h), _bgTex);

            float cx = x + 14;
            float cy = y + 12;
            float lineH = 28;

            GUI.Label(new Rect(cx, cy, w - 24, lineH), "NES Cartridge Hand Adjuster", _headerStyle);
            cy += lineH + 4;

            GUI.Label(new Rect(cx, cy, w - 24, lineH), _statusText, _labelStyle);
            cy += lineH + 4;

            string modeColor = _mode == AdjustMode.Position ? "#88ff88" :
                               _mode == AdjustMode.Rotation ? "#88ccff" : "#ffcc88";
            GUI.Label(new Rect(cx, cy, w - 24, lineH),
                $"<color={modeColor}>Mode: {_mode}</color>  [Numpad / to cycle]", _modeStyle);
            cy += lineH + 4;

            string posHL = _mode == AdjustMode.Position ? "<b>" : "";
            string posHR = _mode == AdjustMode.Position ? "</b>" : "";
            GUI.Label(new Rect(cx, cy, w - 24, lineH),
                $"{posHL}Position: ({_position.x:F3}, {_position.y:F3}, {_position.z:F3})  step={_posStep:F3}{posHR}", _modeStyle);
            cy += lineH;

            string rotHL = _mode == AdjustMode.Rotation ? "<b>" : "";
            string rotHR = _mode == AdjustMode.Rotation ? "</b>" : "";
            GUI.Label(new Rect(cx, cy, w - 24, lineH),
                $"{rotHL}Rotation: ({_rotation.x:F1}, {_rotation.y:F1}, {_rotation.z:F1})  step={_rotStep:F1}{rotHR}", _modeStyle);
            cy += lineH;

            string sclHL = _mode == AdjustMode.Scale ? "<b>" : "";
            string sclHR = _mode == AdjustMode.Scale ? "</b>" : "";
            GUI.Label(new Rect(cx, cy, w - 24, lineH),
                $"{sclHL}Scale: {_scale:F4}  step={_scaleStep:F4}{sclHR}", _modeStyle);
            cy += lineH + 8;

            GUI.Label(new Rect(cx, cy, w - 24, lineH), "Numpad 4/6=X  8/2=Y  7/9=Z  /=mode  +/-=step  *=toggle", _labelStyle);
        }

        private void EnsureStyles()
        {
            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1);
                _bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.92f));
                _bgTex.Apply();
                _bgTex.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_labelStyle != null) return;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 18;
            _labelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontSize = 22;
            _headerStyle.fontStyle = FontStyle.Bold;
            _headerStyle.normal.textColor = Color.white;

            _modeStyle = new GUIStyle(GUI.skin.label);
            _modeStyle.fontSize = 18;
            _modeStyle.richText = true;
            _modeStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
