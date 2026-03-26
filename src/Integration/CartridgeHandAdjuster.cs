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
            {
                HideHands(player);
                // Re-apply label every frame in case the game resets the material
                TryApplyHeldLabel(player, itemName);
            }
        }

        /// <summary>
        /// Finds the held cartridge model on the player and applies the label texture.
        /// Returns true if the label was applied (or confirmed no sticker to apply to),
        /// false if the model isn't ready yet (will retry next frame).
        /// </summary>
        private bool TryApplyHeldLabel(EntityPlayerLocal player, string itemName)
        {
            // Search the player hierarchy for any transform named "sticker_3"
            // (part of the cartridge prefab). The held item model uses MeshRenderers.
            var sticker = FindInHierarchy(player.transform, "sticker_3");
            if (sticker == null) return false;

            CartridgeLabelManager.ApplyLabel(sticker.parent ?? sticker, itemName);
            return true;
        }

        private static Transform FindInHierarchy(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindInHierarchy(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
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
