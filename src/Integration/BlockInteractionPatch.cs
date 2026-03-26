using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SevenNes.Integration
{
    // === BlockPowered patches (nesTV uses Class="Powered") ===

    [HarmonyPatch(typeof(BlockPowered))]
    [HarmonyPatch("OnBlockActivated")]
    [HarmonyPatch(new[] { typeof(string), typeof(WorldBase), typeof(int), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
    public class BlockPoweredActivationPatch
    {
        static bool Prefix(string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
        {
            Block block = _blockValue.Block;
            if (block == null || block.GetBlockName() != "nesTV") return true;

            var window = NesEmulatorWindow.Instance;
            bool isPowered = PowerHelper.IsPowered(_world, _cIdx, _blockPos);

            switch (_commandName)
            {
                case "play":
                    if (!isPowered) return false;
                    window.HandleActivate(_blockPos, _blockValue.rotation);
                    return false;
                case "turn_on":
                    if (!isPowered) return false;
                    window.HandleTurnOn(_blockPos, _blockValue.rotation);
                    return false;
                case "turn_off":
                    window.HandleTurnOff(_blockPos, _blockValue.rotation);
                    return false;
                case "controls":
                    window.HandleControls(_blockPos, _blockValue.rotation);
                    return false;
                default:
                    return true; // Let BlockPowered handle "take", wire tool, etc.
            }
        }
    }

    [HarmonyPatch(typeof(BlockPowered))]
    [HarmonyPatch("GetActivationText")]
    public class BlockPoweredActivationTextPatch
    {
        static bool Prefix(BlockPowered __instance, ref string __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() != "nesTV") return true;

            bool isPowered = PowerHelper.IsPowered(_world, _clrIdx, _blockPos);

            // Auto-show screen quad when player looks at a powered TV
            NesEmulatorWindow.Instance.AutoShowScreen(_blockPos, _blockValue.rotation, isPowered);

            if (isPowered)
                __result = "Press [action:activate] to use NES TV";
            else
                __result = "No Power - Connect with Wire Tool";
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockPowered))]
    [HarmonyPatch("HasBlockActivationCommands")]
    public class BlockPoweredHasActivationPatch
    {
        static bool Prefix(BlockPowered __instance, ref bool __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() == "nesTV")
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BlockPowered))]
    [HarmonyPatch("GetBlockActivationCommands")]
    public class BlockPoweredGetActivationCommandsPatch
    {
        static bool Prefix(BlockPowered __instance, ref BlockActivationCommand[] __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() != "nesTV") return true;

            ModInit.EnsureIconsInjected();

            bool isPowered = PowerHelper.IsPowered(_world, _clrIdx, _blockPos);
            var window = NesEmulatorWindow.Instance;
            bool tvOn = window.IsTvOn;
            bool hasRom = window.HasLoadedRom;

            if (isPowered)
            {
                __result = new BlockActivationCommand[]
                {
                    new BlockActivationCommand("play", "electric_switch", true),
                    new BlockActivationCommand(tvOn ? "turn_off" : "turn_on", "electric_switch", true),
                    new BlockActivationCommand("controls", "electric_switch", true),
                    new BlockActivationCommand("take", "hand", true)
                };
            }
            else
            {
                // No power - only show take and controls
                __result = new BlockActivationCommand[]
                {
                    new BlockActivationCommand("play", "electric_switch", true),
                    new BlockActivationCommand("controls", "electric_switch", true),
                    new BlockActivationCommand("take", "hand", true)
                };
            }
            return false;
        }
    }

    // === Fallback patches on Block base class (in case BlockPowered doesn't override) ===

    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("OnBlockActivated")]
    [HarmonyPatch(new[] { typeof(string), typeof(WorldBase), typeof(int), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
    public class BlockActivationPatch
    {
        static bool Prefix(string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
        {
            Block block = _blockValue.Block;
            if (block == null || block.GetBlockName() != "nesTV") return true;
            if (block is BlockPowered) return true; // Handled by BlockPowered patch

            var window = NesEmulatorWindow.Instance;

            switch (_commandName)
            {
                case "play":
                    window.HandleActivate(_blockPos, _blockValue.rotation);
                    return false;
                case "turn_on":
                    window.HandleTurnOn(_blockPos, _blockValue.rotation);
                    return false;
                case "turn_off":
                    window.HandleTurnOff(_blockPos, _blockValue.rotation);
                    return false;
                case "controls":
                    window.HandleControls(_blockPos, _blockValue.rotation);
                    return false;
                default:
                    return true;
            }
        }
    }

    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("GetActivationText")]
    public class BlockActivationTextPatch
    {
        static bool Prefix(Block __instance, ref string __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() != "nesTV") return true;
            if (__instance is BlockPowered) return true; // Handled by BlockPowered patch

            __result = "Press [action:activate] to use NES TV";
            return false;
        }
    }

    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("HasBlockActivationCommands")]
    public class BlockHasActivationPatch
    {
        static bool Prefix(Block __instance, ref bool __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() != "nesTV") return true;
            if (__instance is BlockPowered) return true;

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("GetBlockActivationCommands")]
    public class BlockGetActivationCommandsPatch
    {
        static bool Prefix(Block __instance, ref BlockActivationCommand[] __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() != "nesTV") return true;
            if (__instance is BlockPowered) return true;

            ModInit.EnsureIconsInjected();

            var window = NesEmulatorWindow.Instance;
            bool tvOn = window.IsTvOn;
            bool hasRom = window.HasLoadedRom;

            __result = new BlockActivationCommand[]
            {
                new BlockActivationCommand("play", "electric_switch", true),
                new BlockActivationCommand(tvOn ? "turn_off" : "turn_on", "electric_switch", true),
                new BlockActivationCommand("controls", "electric_switch", true),
                new BlockActivationCommand("take", "hand", true)
            };
            return false;
        }
    }

    // === nesConsole collider fix: shrink from full-block to model-sized ===

    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("OnBlockEntityTransformAfterActivated")]
    public class NesConsoleColliderPatch
    {
        static void Postfix(Block __instance, WorldBase _world, Vector3i _blockPos, int _cIdx, BlockValue _blockValue, BlockEntityData _ebcd)
        {
            if (__instance.GetBlockName() != "nesConsole") return;
            if (_ebcd?.transform == null) return;

            var boxCol = _ebcd.transform.GetComponent<BoxCollider>();
            if (boxCol == null) return;

            // Calculate tight bounds from all child mesh renderers
            var renderers = _ebcd.transform.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Convert world bounds to local space of the root transform
            var localCenter = _ebcd.transform.InverseTransformPoint(bounds.center);
            var localSize = _ebcd.transform.InverseTransformVector(bounds.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

            boxCol.center = localCenter;
            boxCol.size = localSize;
        }
    }

    // === Cartridge Helper ===
    public static class CartridgeHelper
    {
        /// <summary>
        /// Searches blocks within 1 block of the nesTV for a nesConsole with a cartridge inserted.
        /// Returns the cartridge item name (e.g. "nesCart_ContraUSA") or null if none found.
        /// </summary>
        public static string FindNearbyCartridge(WorldBase world, int clrIdx, Vector3i tvPos)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dy == 0 && dz == 0) continue;
                var pos = new Vector3i(tvPos.x + dx, tvPos.y + dy, tvPos.z + dz);
                var bv = world.GetBlock(pos);
                if (bv.Block == null || bv.Block.GetBlockName() != "nesConsole") continue;

                var te = world.GetTileEntity(clrIdx, pos) as TileEntityLootContainer;
                if (te == null) continue;

                if (te.items != null && te.items.Length > 0 && !te.items[0].IsEmpty())
                {
                    var itemClass = te.items[0].itemValue.ItemClass;
                    if (itemClass != null)
                    {
                        string name = itemClass.GetItemName();
                        if (name.StartsWith("nesCart_"))
                            return name;
                    }
                }
            }
            return null;
        }
    }

    // === Redirect looting window to nesConsole custom window group ===

    // Track when a nesConsole block is being activated so the redirect knows to fire.
    // Also handles quick-insert: left-click (default "open" command) while holding a
    // nesCart_* item inserts the cartridge directly into the console's slot.
    [HarmonyPatch]
    public class NesConsoleActivationTracker
    {
        static MethodBase TargetMethod()
        {
            // BlockSecureLoot overrides OnBlockActivated; patch it directly
            var type = AccessTools.TypeByName("BlockSecureLoot");
            if (type == null) return null;
            return AccessTools.Method(type, "OnBlockActivated",
                new[] { typeof(string), typeof(WorldBase), typeof(int),
                        typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) });
        }

        static void Prefix(BlockValue _blockValue)
        {
            LootWindowNesConsoleRedirectPatch.IsNesConsoleOpening =
                _blockValue.Block?.GetBlockName() == "nesConsole";
        }
    }

    // Redirect GUIWindowManager.Open("looting") to "nesConsole" when the flag is set.
    // This fires BEFORE the window group initializes, so the tile entity handoff is intact.
    [HarmonyPatch]
    public class LootWindowNesConsoleRedirectPatch
    {
        internal static bool IsNesConsoleOpening;

        static MethodBase TargetMethod()
        {
            // Find the GUIWindowManager.Open overload that BlockSecureLoot uses
            var t = typeof(GUIWindowManager);
            var m = AccessTools.Method(t, "Open", new[] { typeof(string), typeof(bool) });
            if (m != null) return m;

            // Fallback: find first Open method with a string first parameter
            foreach (var method in t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "Open") continue;
                var parameters = method.GetParameters();
                if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string))
                    return method;
            }
            return null;
        }

        static void Prefix(ref string __0)
        {
            if (__0 == "looting" && IsNesConsoleOpening)
            {
                IsNesConsoleOpening = false;
                __0 = "nesConsole";
            }
        }
    }

    // === Power Helper ===
    public static class PowerHelper
    {
        public static bool IsPowered(WorldBase world, int clrIdx, Vector3i blockPos)
        {
            try
            {
                var te = world.GetTileEntity(clrIdx, blockPos) as TileEntityPowered;
                if (te == null) return false;
                return te.IsPowered;
            }
            catch
            {
                return false;
            }
        }
    }
}
