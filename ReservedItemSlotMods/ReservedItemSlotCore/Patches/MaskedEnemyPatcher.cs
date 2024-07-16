using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore.Patches;
using UnityEngine;
using Unity.Netcode;
using ReservedItemSlotCore.Data;
using GameNetcodeStuff;
using ReservedItemSlotCore.Config;

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    internal class MaskedEnemyPatcher
    {
        //public static Dictionary<MaskedPlayerEnemy, MaskedEnemyData> heldReservedItemsByMaskedEnemy = new Dictionary<MaskedPlayerEnemy, List<GameObject>>();
        //public static HashSet<MaskedPlayerEnemy> spawnedEnemies = new HashSet<MaskedPlayerEnemy>();


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "Awake")]
        [HarmonyPrefix]
        public static void InitMaskedEnemy(MaskedPlayerEnemy __instance)
        {
            if (!ConfigSettings.showReservedItemsHolsteredMaskedEnemy.Value)
                return;

            if (!MaskedEnemyData.allMaskedEnemyData.ContainsKey(__instance))
                MaskedEnemyData.allMaskedEnemyData.Add(__instance, new MaskedEnemyData(__instance));
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "OnDestroy")]
        [HarmonyPrefix]
        public static void OnDestroy(MaskedPlayerEnemy __instance)
        {
            if (MaskedEnemyData.allMaskedEnemyData.TryGetValue(__instance, out var maskedEnemyData))
            {
                maskedEnemyData.DestroyEquippedItems();
                MaskedEnemyData.allMaskedEnemyData.Remove(__instance);
            }
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "Update")]
        [HarmonyPostfix]
        public static void Update(MaskedPlayerEnemy __instance)
        {
            if (!ConfigSettings.showReservedItemsHolsteredMaskedEnemy.Value)
                return;

            if (!MaskedEnemyData.allMaskedEnemyData.TryGetValue(__instance, out var maskedEnemyData))
                return;

            if (maskedEnemyData.originallyMimickingPlayer == null && maskedEnemyData.maskedEnemy.mimickingPlayer != null)
                AddReservedItemsToMaskedEnemy(__instance);
        }


        public static void AddReservedItemsToMaskedEnemy(MaskedPlayerEnemy maskedEnemy)
        {
            if (!ConfigSettings.showReservedItemsHolsteredMaskedEnemy.Value)
                return;

            if (!MaskedEnemyData.allMaskedEnemyData.TryGetValue(maskedEnemy, out var maskedEnemyData))
                return;

            maskedEnemyData.originallyMimickingPlayer = maskedEnemyData.maskedEnemy.mimickingPlayer;
            if (!ReservedPlayerData.allPlayerData.TryGetValue(maskedEnemyData.originallyMimickingPlayer, out var playerData))
            {
                Plugin.LogWarning("Failed to mimic player's equipped reserved items. Could not retrieve player data from: " + maskedEnemyData.originallyMimickingPlayer.playerUsername);
                return;
            }

            for (int i = playerData.reservedHotbarStartIndex; i < Mathf.Min(playerData.reservedHotbarEndIndexExcluded, playerData.playerController.ItemSlots.Length); i++)
            {
                GrabbableObject heldItem = playerData.playerController.ItemSlots[i];
                if (heldItem == null)
                    continue;

                int reservedItemIndex = i - playerData.reservedHotbarStartIndex;
                if (reservedItemIndex < 0 || reservedItemIndex >= SessionManager.unlockedReservedItemSlots.Count)
                {
                    Plugin.LogWarning("Failed to add reserved item to MaskedEnemy. Could not get ReservedItemSlot at index: " + reservedItemIndex + " Item: " + heldItem.itemProperties.itemName + " SlotIndexInInventory: " + i + " ReservedHotbarStartIndex: " + playerData.reservedHotbarStartIndex);
                    continue;
                }
                var reservedItemSlot = SessionManager.unlockedReservedItemSlots[reservedItemIndex];
                var reservedItem = reservedItemSlot.GetReservedItemData(heldItem);
                if (reservedItem.holsteredParentBone == PlayerBone.None)
                    continue;

                Transform holsterParent = maskedEnemyData.boneMap.GetBone(reservedItem.holsteredParentBone);
                if (holsterParent == null)
                {
                    Plugin.LogWarning("Failed to get bone from masked enemy: " + reservedItem.holsteredParentBone.ToString());
                    continue;
                }
                GameObject newObject = GameObject.Instantiate(heldItem.gameObject, holsterParent);
                newObject.transform.localEulerAngles = reservedItem.holsteredRotationOffset;
                newObject.transform.localPosition = reservedItem.holsteredPositionOffset;
                newObject.transform.localScale = heldItem.transform.localScale;
                newObject.layer = 6;

                foreach (var renderer in newObject.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.name.Contains("ScanNode") && !renderer.gameObject.CompareTag("DoNotSet") && !renderer.gameObject.CompareTag("InteractTrigger"))
                        renderer.gameObject.layer = 6;
                }

                if (heldItem is FlashlightItem)
                {
                    foreach (var light in newObject.GetComponentsInChildren<Light>())
                        light.enabled = false;
                }
                else
                {
                    foreach (var light in newObject.GetComponentsInChildren<Light>())
                        light.enabled = true;
                }

                GrabbableObject grabbableObjectData = newObject.GetComponentInChildren<GrabbableObject>();

                if (grabbableObjectData != null)
                {
                    grabbableObjectData.playerHeldBy = null;
                    var flashlightItem = grabbableObjectData as FlashlightItem;
                    if (flashlightItem != null)
                    {
                        flashlightItem.flashlightBulb.enabled = true;
                        flashlightItem.flashlightBulbGlow.enabled = true;
                        flashlightItem.flashlightMesh.sharedMaterials[1] = flashlightItem.bulbLight;
                    }
                    ReservedItemsPatcher.ForceEnableItemMesh(grabbableObjectData, true);
                    grabbableObjectData.EnablePhysics(false);
                }
                GameObject.DestroyImmediate(newObject.GetComponentInChildren<NetworkObject>());
                foreach (var collider in newObject.GetComponentsInChildren<Collider>())
                    GameObject.DestroyImmediate(collider);
                foreach (var monoBehaviour in newObject.GetComponentsInChildren<MonoBehaviour>())
                    GameObject.DestroyImmediate(monoBehaviour);
            }
        }
    }
}