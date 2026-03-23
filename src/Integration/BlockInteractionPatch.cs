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
                case "choose_game":
                    if (!isPowered) return false;
                    window.HandleChooseGame(_blockPos, _blockValue.rotation);
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
                    new BlockActivationCommand("choose_game", "nes_cartridge", true),
                    new BlockActivationCommand(tvOn ? "turn_off" : "turn_on", "electric_switch", tvOn || hasRom),
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
                case "choose_game":
                    window.HandleChooseGame(_blockPos, _blockValue.rotation);
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
                new BlockActivationCommand("choose_game", "nes_cartridge", true),
                new BlockActivationCommand(tvOn ? "turn_off" : "turn_on", "electric_switch", tvOn || hasRom),
                new BlockActivationCommand("controls", "electric_switch", true),
                new BlockActivationCommand("take", "hand", true)
            };
            return false;
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
