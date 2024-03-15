using GameNetcodeStuff;
using HarmonyLib;
using ReservedItemSlotCore.Data;
using ReservedItemSlotCore.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    internal static class ReservedItemsPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static bool ignoreMeshOverride = false;


        [HarmonyPatch(typeof(GrabbableObject), "PocketItem")]
        [HarmonyPostfix]
        public static void OnPocketReservedItem(GrabbableObject __instance)
        {
            if (__instance.playerHeldBy == null)
                return;

            if (ReservedPlayerData.allPlayerData.TryGetValue(__instance.playerHeldBy, out var playerData))
            {
                if (SessionManager.TryGetUnlockedItemData(__instance, out var itemData))
                {
                    if (playerData.IsItemInReservedItemSlot(__instance))
                    {
                        if (itemData.showOnPlayerWhileHolstered)
                        {
                            foreach (var renderer in __instance.GetComponentsInChildren<MeshRenderer>())
                            {
                                if (!renderer.name.Contains("ScanNode") && !renderer.gameObject.CompareTag("DoNotSet") && !renderer.gameObject.CompareTag("InteractTrigger"))
                                    renderer.gameObject.layer = playerData.isLocalPlayer ? 23 : 6;
                            }
                            __instance.parentObject = playerData.boneMap.GetBone(itemData.holsteredParentBone);
                            ForceEnableItemMesh(__instance, true);
                        }
                    }
                }
            }
        }


        [HarmonyPatch(typeof(GrabbableObject), "EquipItem")]
        [HarmonyPostfix]
        public static void OnEquipReservedItem(GrabbableObject __instance)
        {
            if (__instance.playerHeldBy == null)
                return;

            if (ReservedPlayerData.allPlayerData.TryGetValue(__instance.playerHeldBy, out var playerData) && SessionManager.IsReservedItem(__instance))
            {
                foreach (var renderer in __instance.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.name.Contains("ScanNode") && !renderer.gameObject.CompareTag("DoNotSet") && !renderer.gameObject.CompareTag("InteractTrigger"))
                        renderer.gameObject.layer = 6;
                }
                __instance.parentObject = playerData.isLocalPlayer ? __instance.playerHeldBy.localItemHolder : __instance.playerHeldBy.serverItemHolder;
            }
        }


        [HarmonyPatch(typeof(GrabbableObject), "DiscardItem")]
        [HarmonyPostfix]
        public static void ResetReservedItemLayer(GrabbableObject __instance)
        {
            if (SessionManager.IsReservedItem(__instance))
            {
                foreach (var renderer in __instance.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.name.Contains("ScanNode") && !renderer.gameObject.CompareTag("DoNotSet") && !renderer.gameObject.CompareTag("InteractTrigger"))
                        renderer.gameObject.layer = 6;
                }
            }
        }


        [HarmonyPatch(typeof(GrabbableObject), "LateUpdate")]
        [HarmonyPostfix]
        public static void SetPositionOffset(GrabbableObject __instance)
        {
            if (__instance.playerHeldBy == null || __instance.parentObject == null)
                return;

            if (ReservedPlayerData.allPlayerData.TryGetValue(__instance.playerHeldBy, out var playerData) && SessionManager.TryGetUnlockedItemData(__instance, out var reservedItemData))
            {
                if (playerData.IsItemInReservedItemSlot(__instance) && reservedItemData.showOnPlayerWhileHolstered && __instance != playerData.currentSelectedItem)
                {
                    Transform parent = __instance.parentObject.transform;
                    __instance.transform.rotation = __instance.parentObject.transform.rotation * Quaternion.Euler(reservedItemData.holsteredRotationOffset);
                    __instance.transform.position = parent.position + parent.rotation * reservedItemData.holsteredPositionOffset;
                }
            }
        }


        [HarmonyPatch(typeof(GrabbableObject), "EnableItemMeshes")]
        [HarmonyPrefix]
        public static void OnEnableItemMeshes(ref bool enable, GrabbableObject __instance)
        {
            if (__instance.playerHeldBy == null || ignoreMeshOverride || !ReservedPlayerData.allPlayerData.TryGetValue(__instance.playerHeldBy, out var playerData))
            {
                ignoreMeshOverride = false;
                return;
            }

            if (SessionManager.TryGetUnlockedItemData(__instance, out var reservedItemData) && playerData.IsItemInReservedItemSlot(__instance) && reservedItemData.showOnPlayerWhileHolstered && playerData.currentSelectedItem != __instance && !PlayerPatcher.ReservedItemIsBeingGrabbed(__instance))
                enable = true;
            ignoreMeshOverride = false;
        }


        public static void ForceEnableItemMesh(GrabbableObject grabbableObject, bool enabled)
        {
            ignoreMeshOverride = true;
            grabbableObject.EnableItemMeshes(enabled);
        }
    }
}
