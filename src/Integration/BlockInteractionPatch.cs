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
            // Check if the activated block is our NES TV
            Block block = _blockValue.Block;
            if (block == null) return true;

            if (block.GetBlockName() == "nesTV")
            {
                // Open the NES emulator window
                NesEmulatorWindow.Instance.Open();
                return false; // Don't run the original method
            }

            return true; // Run original for other blocks
        }
    }

    // Also patch GameManager.ChangeBlocks or similar if needed for custom block class
    // Alternative approach: patch the general interaction handler
    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("GetActivationText")]
    public class BlockActivationTextPatch
    {
        static bool Prefix(Block __instance, ref string __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() == "nesTV")
            {
                __result = "Press <E> to play NES";
                return false;
            }
            return true;
        }
    }

    // Ensure HasBlockActivationCommands returns true for our block
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

    // Return our custom activation command
    [HarmonyPatch(typeof(Block))]
    [HarmonyPatch("GetBlockActivationCommands")]
    public class BlockGetActivationCommandsPatch
    {
        static bool Prefix(Block __instance, ref BlockActivationCommand[] __result, WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            if (__instance.GetBlockName() == "nesTV")
            {
                __result = new BlockActivationCommand[]
                {
                    new BlockActivationCommand("play", "electric_switch", true)
                };
                return false;
            }
            return true;
        }
    }
}
