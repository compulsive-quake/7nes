using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SevenNes.Integration
{
    /// <summary>
    /// Loads cartridge label PNGs from Roms/labels/ at mod init and provides
    /// textures keyed by item name for runtime application to cartridge models.
    /// </summary>
    public static class CartridgeLabelManager
    {
        private static readonly Dictionary<string, Texture2D> _labelTextures = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Scans Roms/labels/ for PNG files matching ROM filenames and caches them
        /// as Texture2D objects keyed by the corresponding item name.
        /// </summary>
        public static void LoadLabels(string romsPath)
        {
            _labelTextures.Clear();

            var labelsDir = Path.Combine(romsPath, "label");
            if (!Directory.Exists(labelsDir))
            {
                Log.Out("[7nes] No labels directory found at " + labelsDir);
                return;
            }

            var romFiles = Directory.GetFiles(romsPath, "*.nes");
            int count = 0;

            foreach (var romFile in romFiles)
            {
                string romName = Path.GetFileNameWithoutExtension(romFile);
                string itemName = NesCartridgeItems.GetItemName(romFile);
                string labelPath = Path.Combine(labelsDir, romName + ".png");
                if (!File.Exists(labelPath))
                    labelPath = Path.Combine(labelsDir, romName + ".jpg");

                if (File.Exists(labelPath))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(File.ReadAllBytes(labelPath));
                    tex.filterMode = FilterMode.Bilinear;
                    tex.Apply();
                    _labelTextures[itemName] = tex;
                    count++;
                }
            }

            Log.Out($"[7nes] Loaded {count} cartridge label(s) from {labelsDir}");
        }

        /// <summary>
        /// Returns the label texture for the given item name, or null if no label exists.
        /// </summary>
        public static Texture2D GetLabelTexture(string itemName)
        {
            if (itemName != null && _labelTextures.TryGetValue(itemName, out var tex))
                return tex;
            return null;
        }

        /// <summary>
        /// Finds the sticker renderer under a cartridge model root and applies
        /// the label texture for the given item. Uses renderer.material to create
        /// a per-instance copy so other cartridges are not affected.
        /// If no label exists for this item, the sticker is hidden.
        /// </summary>
        public static void ApplyLabel(Transform root, string itemName)
        {
            var stickerTransform = FindChildRecursive(root, "sticker_3");
            if (stickerTransform == null) return;

            var labelTex = GetLabelTexture(itemName);
            var renderers = stickerTransform.GetComponentsInChildren<Renderer>(true);

            foreach (var r in renderers)
            {
                if (labelTex != null)
                {
                    // Check if already correct to avoid per-frame work
                    if (r.enabled && r.sharedMaterial != null && r.sharedMaterial.mainTexture == labelTex)
                        continue;

                    r.enabled = true;
                    var mat = r.material;
                    var standardShader = Shader.Find("Standard");
                    if (standardShader != null)
                    {
                        mat.shader = standardShader;
                        mat.color = Color.white;
                        mat.SetFloat("_Mode", 0);
                        mat.SetFloat("_Metallic", 0f);
                        mat.SetFloat("_Glossiness", 0.3f);
                    }
                    mat.mainTexture = labelTex;
                }
                else
                {
                    r.enabled = false;
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;
                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
