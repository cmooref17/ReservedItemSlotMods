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

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    public class MaskedEnemyPatcher
    {
        //public static Dictionary<MaskedPlayerEnemy, MaskedEnemyData> heldReservedItemsByMaskedEnemy = new Dictionary<MaskedPlayerEnemy, List<GameObject>>();
        //public static HashSet<MaskedPlayerEnemy> spawnedEnemies = new HashSet<MaskedPlayerEnemy>();


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "Awake")]
        [HarmonyPrefix]
        public static void InitMaskedEnemy(MaskedPlayerEnemy __instance)
        {
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
            if (!MaskedEnemyData.allMaskedEnemyData.TryGetValue(__instance, out var maskedEnemyData))
                return;

            if (maskedEnemyData.originallyMimickingPlayer == null && maskedEnemyData.maskedEnemy.mimickingPlayer != null)
                AddReservedItemsToMaskedEnemy(__instance);
        }


        public static void AddReservedItemsToMaskedEnemy(MaskedPlayerEnemy maskedEnemy)
        {
            if (!MaskedEnemyData.allMaskedEnemyData.TryGetValue(maskedEnemy, out var maskedEnemyData))
                return;

            maskedEnemyData.originallyMimickingPlayer = maskedEnemyData.maskedEnemy.mimickingPlayer;
            if (!ReservedPlayerData.allPlayerData.TryGetValue(maskedEnemyData.originallyMimickingPlayer, out var playerData))
            {
                Plugin.LogWarning("Failed to mimic player's equipped reserved items. Could not retrieve player data from: " + maskedEnemyData.originallyMimickingPlayer.playerUsername);
                return;
            }

            for (int i = playerData.reservedHotbarStartIndex; i < playerData.reservedHotbarEndIndexExcluded; i++)
            {
                GrabbableObject heldItem = playerData.playerController.ItemSlots[i];
                if (heldItem == null)
                    continue;

                int reservedItemIndex = i - playerData.reservedHotbarStartIndex;
                var reservedItemSlot = SessionManager.unlockedReservedItemSlots[reservedItemIndex];
                var reservedItem = reservedItemSlot.GetReservedItem(heldItem);
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
                    var flashlightItem = grabbableObjectData as FlashlightItem;
                    if (flashlightItem != null)
                    {
                        if (flashlightItem.insertedBattery != null)
                            flashlightItem.insertedBattery.charge = 100;
                        flashlightItem.SwitchFlashlight(true);
                    }
                    grabbableObjectData.playerHeldBy = null;
                    grabbableObjectData.EnableItemMeshes(true);
                    if (grabbableObjectData.mainObjectRenderer != null)
                        grabbableObjectData.mainObjectRenderer.enabled = true;
                    grabbableObjectData.EnablePhysics(false);
                }
                GameObject.DestroyImmediate(newObject.GetComponentInChildren<NetworkObject>());
                foreach (var collider in newObject.GetComponentsInChildren<Collider>())
                    GameObject.DestroyImmediate(collider);
                foreach (var monoBehaviour in newObject.GetComponentsInChildren<MonoBehaviour>())
                    GameObject.DestroyImmediate(monoBehaviour);
            }
        }

        /*
        [HarmonyPatch(typeof(MaskedPlayerEnemy), "LateUpdate")]
        [HarmonyPostfix]
        public static void LateUpdate(MaskedPlayerEnemy __instance)
        {
            if (!spawnedEnemies.Contains(__instance) && SyncManager.syncReservedItemsList.Contains(Plugin.proFlashlightInfo) && !__instance.isEnemyDead && !heldReservedItemsByMaskedEnemy.ContainsKey(__instance) && __instance.mimickingPlayer != null && ReservedPlayerData.allPlayerData.TryGetValue(__instance.mimickingPlayer, out var playerData))
            {
                spawnedEnemies.Add(__instance);
                var flashlight = FlashlightPatcher.GetReservedFlashlight(playerData.playerController);
                if (flashlight != null)
                {
                    Plugin.LogWarning("OnMaskedEnemySpawn - MimickingPlayer: " + playerData.playerController.name + " - Spawning flashlight object on enemy.");

                    GameObject newFlashlightObject = new GameObject("ReservedFlashlight [MaskedEnemy]");
                    var lights = flashlight.GetComponentsInChildren<Light>();
                    var meshRenderer = flashlight.mainObjectRenderer;
                    foreach (var light in lights)
                    {
                        var newLight = GameObject.Instantiate(light.gameObject, light.transform.localPosition, light.transform.localRotation, newFlashlightObject.transform).GetComponent<Light>();
                        newLight.enabled = true;
                        newLight.gameObject.layer = 6;
                    }
                    var newMeshRenderer = GameObject.Instantiate(meshRenderer.gameObject, meshRenderer.transform.localPosition, meshRenderer.transform.localRotation, newFlashlightObject.transform).GetComponent<MeshRenderer>();
                    newMeshRenderer.enabled = true;
                    newMeshRenderer.gameObject.layer = 6;
                    newFlashlightObject.transform.localScale = flashlight.transform.localScale;

                    heldReservedItemsByMaskedEnemy.Add(__instance, newFlashlightObject);
                }
            }

            if (heldReservedItemsByMaskedEnemy.TryGetValue(__instance, out var flashlightObject))
            {
                if (__instance.isEnemyDead)
                {
                    Plugin.LogWarning("Destroying flashlight. Enemy dead.");
                    GameObject.DestroyImmediate(flashlightObject);
                    spawnedEnemies.Remove(__instance);
                    heldReservedItemsByMaskedEnemy.Remove(__instance);
                }
                else
                {
                    Transform parent = __instance.eye.parent.parent;
                    flashlightObject.transform.rotation = parent.rotation * Quaternion.Euler(FlashlightPatcher.playerShoulderRotationOffset);
                    flashlightObject.transform.position = parent.position + parent.rotation * FlashlightPatcher.playerShoulderPositionOffset;
                }
            }
        }
        */
    }
}