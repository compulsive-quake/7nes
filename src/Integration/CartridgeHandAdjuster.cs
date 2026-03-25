using System.Collections.Generic;
using UnityEngine;

namespace SevenNes.Integration
{
    public class CartridgeHandAdjuster : MonoBehaviour
    {
        private static CartridgeHandAdjuster _instance;
        public static CartridgeHandAdjuster Instance => _instance;

        // Track held item to detect switches
        private int _lastHeldItemIdx = -1;
        private string _lastHeldItemName;
        private bool _holdingCartridge;

        // Hand hiding
        private bool _handsHidden;
        private List<SkinnedMeshRenderer> _hiddenRenderers = new List<SkinnedMeshRenderer>();

        void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void LateUpdate()
        {
            if (!IsInGame()) return;

            var player = GameManager.Instance.World.GetPrimaryPlayer();
            var inv = player.inventory;
            int idx = inv.holdingItemIdx;
            string itemName = inv.holdingItem?.Name;

            // Detect held item change
            if (idx != _lastHeldItemIdx || itemName != _lastHeldItemName)
            {
                ShowHands();
                _lastHeldItemIdx = idx;
                _lastHeldItemName = itemName;
                _holdingCartridge = itemName != null && itemName.StartsWith("nesCart_");
            }

            if (_holdingCartridge)
                HideHands(player);
        }

        private void HideHands(EntityPlayerLocal player)
        {
            if (_handsHidden) return;

            // Walk the entire player hierarchy and disable all SkinnedMeshRenderers
            // (arms/hands). The cartridge model uses regular MeshRenderers so it stays visible.
            var skinnedRenderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            foreach (var smr in skinnedRenderers)
            {
                if (smr.enabled)
                {
                    smr.enabled = false;
                    _hiddenRenderers.Add(smr);
                }
            }

            if (_hiddenRenderers.Count > 0)
                _handsHidden = true;
        }

        private void ShowHands()
        {
            if (!_handsHidden) return;

            foreach (var smr in _hiddenRenderers)
            {
                if (smr != null)
                    smr.enabled = true;
            }

            _hiddenRenderers.Clear();
            _handsHidden = false;
        }

        private static bool IsInGame()
        {
            return GameManager.Instance != null
                && GameManager.Instance.World != null
                && GameManager.Instance.World.GetPrimaryPlayer() != null;
        }

        void OnDestroy()
        {
            ShowHands();
            if (_instance == this) _instance = null;
        }
    }
}
