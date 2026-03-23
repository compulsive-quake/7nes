using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SevenNes.Integration
{
    public class ModInit : IModApi
    {
        public static string ModPath { get; private set; }
        public static string RomsPath { get; private set; }

        private static readonly Dictionary<string, byte[]> iconData = new Dictionary<string, byte[]>();
        private static bool _iconsInjected;

        public void InitMod(Mod _modInstance)
        {
            ModPath = _modInstance.Path;
            RomsPath = Path.Combine(ModPath, "Roms");

            if (!Directory.Exists(RomsPath))
                Directory.CreateDirectory(RomsPath);

            LoadCustomIconData();

            var harmony = new Harmony("com.7nes.nesemulator");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Out("[7nes] NES Emulator TV mod loaded!");
            Log.Out($"[7nes] ROM directory: {RomsPath}");

            var romFiles = Directory.GetFiles(RomsPath, "*.nes");
            Log.Out($"[7nes] Found {romFiles.Length} ROM files");
        }

        private void LoadCustomIconData()
        {
            var iconPath = Path.Combine(ModPath, "UIAtlases", "UIAtlas", "nes_cartridge.png");
            if (File.Exists(iconPath))
            {
                iconData["ui_game_symbol_nes_cartridge"] = File.ReadAllBytes(iconPath);
                Log.Out("[7nes] Loaded cartridge icon for atlas injection");
            }
            else
            {
                Log.Warning("[7nes] Cartridge icon not found: " + iconPath);
            }
        }

        public static void InjectCustomIcons()
        {
            if (iconData.Count == 0) return;
            if (_iconsInjected) return;

            try
            {
                // Find the UIAtlas MultiSourceAtlasManager
                var managers = Resources.FindObjectsOfTypeAll<MultiSourceAtlasManager>();
                MultiSourceAtlasManager uiAtlasManager = null;
                foreach (var mgr in managers)
                {
                    if (mgr.name == "UIAtlas")
                    {
                        uiAtlasManager = mgr;
                        break;
                    }
                }

                if (uiAtlasManager == null)
                {
                    Log.Error("[7nes] Could not find UIAtlas MultiSourceAtlasManager");
                    return;
                }

                foreach (var kvp in iconData)
                {
                    var spriteName = kvp.Key;
                    var pngData = kvp.Value;

                    // Load texture
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(pngData);
                    tex.Apply();

                    // Create atlas GameObject and UIAtlas component
                    var atlasGo = new GameObject("7nes_" + spriteName);
                    UnityEngine.Object.DontDestroyOnLoad(atlasGo);
                    var atlas = atlasGo.AddComponent<UIAtlas>();

                    // Create material with the texture
                    var mat = new Material(Shader.Find("Unlit/Transparent Colored"));
                    mat.mainTexture = tex;
                    atlas.spriteMaterial = mat;

                    // Add sprite data covering the full texture
                    var spriteData = new UISpriteData();
                    spriteData.name = spriteName;
                    spriteData.SetRect(0, 0, tex.width, tex.height);
                    atlas.spriteList.Add(spriteData);

                    // Register with the UIAtlas manager
                    uiAtlasManager.AddAtlas(atlas, true);

                    Log.Out($"[7nes] Injected custom icon: {spriteName} ({tex.width}x{tex.height})");
                }

                _iconsInjected = true;
            }
            catch (Exception e)
            {
                Log.Error("[7nes] Failed to inject custom icons: " + e.Message);
            }
        }

        /// Re-inject icons if the atlas was rebuilt (e.g., after scene reload)
        public static void EnsureIconsInjected()
        {
            if (iconData.Count == 0) return;

            // Check if our atlas still exists
            var existing = GameObject.Find("7nes_ui_game_symbol_nes_cartridge");
            if (existing != null) return;

            // Atlas was lost, re-inject
            Log.Out("[7nes] Custom icon atlas lost, re-injecting...");
            _iconsInjected = false;
            InjectCustomIcons();
        }
    }

    // Inject our custom icons after the game's XUi has initialized its atlases
    [HarmonyPatch(typeof(XUi))]
    [HarmonyPatch("Init")]
    public class XUiInitPatch
    {
        static void Postfix()
        {
            ModInit.InjectCustomIcons();
        }
    }
}
