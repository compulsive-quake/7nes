using System;
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

        public void InitMod(Mod _modInstance)
        {
            ModPath = _modInstance.Path;
            RomsPath = Path.Combine(ModPath, "Roms");

            if (!Directory.Exists(RomsPath))
                Directory.CreateDirectory(RomsPath);

            var harmony = new Harmony("com.7nes.nesemulator");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Out("[7nes] NES Emulator TV mod loaded!");
            Log.Out($"[7nes] ROM directory: {RomsPath}");

            var romFiles = Directory.GetFiles(RomsPath, "*.nes");
            Log.Out($"[7nes] Found {romFiles.Length} ROM files");
        }
    }
}
