﻿using HarmonyLib;
using ReservedItemSlotCore.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    internal static class SyncAlreadyHeldObjectsPatcher
    {
        [HarmonyPatch(typeof(StartOfRound), "SyncAlreadyHeldObjectsClientRpc")]
        [HarmonyPrefix]
        private static bool SyncAlreadyHeldReservedObjectsClientRpc(ref NetworkObjectReference[] gObjects, ref int[] playersHeldBy, ref int[] itemSlotNumbers, ref int[] isObjectPocketed, int syncWithClient, StartOfRound __instance)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true;

            if (NetworkHelper.IsClientExecStage(__instance) || (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost))
            {
                if (!NetworkHelper.IsClientExecStage(__instance) || (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost) || syncWithClient != (int)NetworkManager.Singleton.LocalClientId)
                    return false;
            }

            bool saveChanges = false;

            var newGObjects = new List<NetworkObjectReference>(gObjects);
            var newPlayersHeldBy = new List<int>(playersHeldBy);
            var newItemSlotNumbers = new List<int>(itemSlotNumbers);
            var newIsObjectPocketed = new List<int>(isObjectPocketed);

            for (int i = itemSlotNumbers.Length - 1; i >= 0; i--)
            {
                if (itemSlotNumbers[i] >= __instance.localPlayerController.ItemSlots.Length)
                {
                    newGObjects.RemoveAt(i);
                    newPlayersHeldBy.RemoveAt(i);
                    newItemSlotNumbers.RemoveAt(i);
                    newIsObjectPocketed.Remove(i);
                    saveChanges = true;
                }
            }

            if (saveChanges)
            {
                gObjects = newGObjects.ToArray();
                playersHeldBy = newPlayersHeldBy.ToArray();
                itemSlotNumbers = newItemSlotNumbers.ToArray();
                isObjectPocketed = newIsObjectPocketed.ToArray();
            }
            return true;
        }
    }
}
