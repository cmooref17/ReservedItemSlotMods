using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore.Patches;
using ReservedFlashlightSlot.Patches;
using UnityEngine;
using Unity.Netcode;
using ReservedItemSlotCore;
/*
namespace ReservedFlashlightSlot.Patches
{
    [HarmonyPatch]
    public class MaskedEnemyPatcher
    {
        public static Dictionary<MaskedPlayerEnemy, GameObject> heldFlashlightsByEnemy = new Dictionary<MaskedPlayerEnemy, GameObject>();
        public static HashSet<MaskedPlayerEnemy> spawnedEnemies = new HashSet<MaskedPlayerEnemy>();


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "OnDestroy")]
        [HarmonyPrefix]
        public static void OnDestroy(MaskedPlayerEnemy __instance)
        {
            if (heldFlashlightsByEnemy.TryGetValue(__instance, out var flashlightObject))
            {
                Plugin.LogWarning("Destroying flashlight. Enemy destroyed.");
                GameObject.DestroyImmediate(flashlightObject);
                spawnedEnemies.Remove(__instance);
            }
            heldFlashlightsByEnemy.Remove(__instance);
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "LateUpdate")]
        [HarmonyPostfix]
        public static void ShowFlashlightOnEnemy(MaskedPlayerEnemy __instance)
        {
            if (!spawnedEnemies.Contains(__instance) && SessionManager.syncReservedItemsList.Contains(Plugin.proFlashlightInfo) && !__instance.isEnemyDead && !heldFlashlightsByEnemy.ContainsKey(__instance) && __instance.mimickingPlayer != null && PlayerPatcher.allPlayerData.TryGetValue(__instance.mimickingPlayer, out var playerData))
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

                    heldFlashlightsByEnemy.Add(__instance, newFlashlightObject);
                }
            }

            if (heldFlashlightsByEnemy.TryGetValue(__instance, out var flashlightObject))
            {
                if (__instance.isEnemyDead)
                {
                    Plugin.LogWarning("Destroying flashlight. Enemy dead.");
                    GameObject.DestroyImmediate(flashlightObject);
                    spawnedEnemies.Remove(__instance);
                    heldFlashlightsByEnemy.Remove(__instance);
                }
                else
                {
                    Transform parent = __instance.eye.parent.parent;
                    flashlightObject.transform.rotation = parent.rotation * Quaternion.Euler(FlashlightPatcher.playerShoulderRotationOffset);
                    flashlightObject.transform.position = parent.position + parent.rotation * FlashlightPatcher.playerShoulderPositionOffset;
                }
            }
        }
    }
}*/