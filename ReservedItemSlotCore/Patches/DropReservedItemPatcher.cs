using GameNetcodeStuff;
using HarmonyLib;
using ReservedItemSlotCore.Data;
using ReservedItemSlotCore.Input;
using ReservedItemSlotCore.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    internal static class DropReservedItemPatcher
    {
        [HarmonyPatch(typeof(PlayerControllerB), "ThrowObjectClientRpc")]
        [HarmonyPostfix]
        private static void OnDiscardHeldReservedItem(bool droppedInElevator, bool droppedInShipRoom, Vector3 targetFloorPosition, NetworkObjectReference grabbedObject, int floorYRot, PlayerControllerB __instance)
        {
            if (!NetworkHelper.IsClientExecStage(__instance))
                return;

            // Swap to another reserved item slot that is not empty, or back to the main hotbar
            if (ReservedPlayerData.allPlayerData.TryGetValue(__instance, out var playerData) && playerData.currentItemSlotIsReserved && playerData.currentlySelectedItem == null)
            {
                if (playerData.GetNumHeldReservedItems() > 0)
                {
                    int swapToIndex = playerData.CallGetNextItemSlot(true);
                    if (!playerData.IsReservedItemSlot(swapToIndex))
                    {
                        if (!playerData.IsReservedItemSlot(ReservedHotbarManager.indexInHotbar))
                            swapToIndex = ReservedHotbarManager.indexInHotbar;
                    }
                    playerData.CallSwitchToItemSlot(swapToIndex);
                }
            }
        }
    }
}
