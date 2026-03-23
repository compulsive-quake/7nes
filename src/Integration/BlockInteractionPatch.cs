using HarmonyLib;
using UnityEngine;

namespace SevenNes.Integration
{
    // Patch the block activation to intercept our NES TV block
    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("OnBlockActivated")]
    [HarmonyPatch(new[] { typeof(string), typeof(WorldBase), typeof(int), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
    public class BlockActivationPatch
    {
        static bool Prefix(string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
        {
            Block block = _blockValue.Block;
            if (block == null || block.GetBlockName() != "nesTV") return true;

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
                default:
                    return true; // Let game handle "take" etc.
            }
        }
    }

    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("GetActivationText")]
    public class BlockActivationTextPatch
    {
        static bool Prefix(Block __instance, ref string __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() == "nesTV")
            {
                __result = "Press [action:activate] to use NES TV";
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("HasBlockActivationCommands")]
    public class BlockHasActivationPatch
    {
        static bool Prefix(Block __instance, ref bool __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() == "nesTV")
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("GetBlockActivationCommands")]
    public class BlockGetActivationCommandsPatch
    {
        static bool Prefix(Block __instance, ref BlockActivationCommand[] __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() != "nesTV") return true;

            var window = NesEmulatorWindow.Instance;
            bool tvOn = window.IsTvOn;
            bool hasRom = window.HasLoadedRom;

            __result = new BlockActivationCommand[]
            {
                new BlockActivationCommand("play", "electric_switch", true),
                new BlockActivationCommand("choose_game", "nes_cartridge", true),
                new BlockActivationCommand(tvOn ? "turn_off" : "turn_on", "electric_switch", tvOn || hasRom),
                new BlockActivationCommand("take", "hand", true)
            };
            return false;
        }
    }
}
