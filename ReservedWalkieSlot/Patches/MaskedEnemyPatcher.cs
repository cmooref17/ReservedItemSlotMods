using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore.Patches;
using ReservedWalkieSlot.Patches;
using UnityEngine;
using Unity.Netcode;

/*
namespace ReservedWalkieSlot.Patches
{
    [HarmonyPatch]
    public class MaskedEnemyPatcher
    {
        public static Dictionary<MaskedPlayerEnemy, GameObject> heldWalkiesByEnemy = new Dictionary<MaskedPlayerEnemy, GameObject>();
        public static HashSet<MaskedPlayerEnemy> spawnedEnemies = new HashSet<MaskedPlayerEnemy>();


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "OnDestroy")]
        [HarmonyPrefix]
        public static void OnDestroy(MaskedPlayerEnemy __instance)
        {
            if (heldWalkiesByEnemy.TryGetValue(__instance, out var walkieObject))
            {
                Plugin.LogWarning("Destroying walkie. Enemy destroyed.");
                GameObject.DestroyImmediate(walkieObject);
                spawnedEnemies.Remove(__instance);
            }
            heldWalkiesByEnemy.Remove(__instance);
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "LateUpdate")]
        [HarmonyPostfix]
        public static void ShowWalkieOnEnemy(MaskedPlayerEnemy __instance)
        {
            if (!spawnedEnemies.Contains(__instance) && SyncManager.syncReservedItemsList.Contains(Plugin.walkieInfo) && !__instance.isEnemyDead && !heldWalkiesByEnemy.ContainsKey(__instance) && __instance.mimickingPlayer != null && PlayerPatcher.allPlayerData.TryGetValue(__instance.mimickingPlayer, out var playerData))
            {
                spawnedEnemies.Add(__instance);
                var walkie = WalkiePatcher.GetReservedWalkie(playerData.playerController);
                if (walkie != null)
                {
                    Plugin.LogWarning("OnMaskedEnemySpawn - MimickingPlayer: " + playerData.playerController.name + " - Spawning walkie object on enemy.");

                    GameObject newWalkieObject = new GameObject("ReservedWalkie [MaskedEnemy]");
                    var lights = walkie.GetComponentsInChildren<Light>();
                    var meshRenderer = walkie.mainObjectRenderer;
                    foreach (var light in lights)
                    {
                        var newLight = GameObject.Instantiate(light.gameObject, light.transform.localPosition, light.transform.localRotation, newWalkieObject.transform).GetComponent<Light>();
                        newLight.enabled = true;
                        newLight.gameObject.layer = 6;
                    }
                    var newMeshRenderer = GameObject.Instantiate(meshRenderer.gameObject, meshRenderer.transform.localPosition, meshRenderer.transform.localRotation, newWalkieObject.transform).GetComponent<MeshRenderer>();
                    newMeshRenderer.enabled = true;
                    newMeshRenderer.gameObject.layer = 6;
                    newWalkieObject.transform.localScale = walkie.transform.localScale;

                    heldWalkiesByEnemy.Add(__instance, newWalkieObject);
                }
            }

            if (heldWalkiesByEnemy.TryGetValue(__instance, out var walkieObject))
            {
                if (__instance.isEnemyDead)
                {
                    Plugin.LogWarning("Destroying walkie. Enemy dead.");
                    GameObject.DestroyImmediate(walkieObject);
                    spawnedEnemies.Remove(__instance);
                    heldWalkiesByEnemy.Remove(__instance);
                }
                else
                {
                    Transform parent = __instance.eye.parent.parent;
                    walkieObject.transform.rotation = parent.rotation * Quaternion.Euler(WalkiePatcher.playerShoulderRotationOffset);
                    walkieObject.transform.position = parent.position + parent.rotation * WalkiePatcher.playerShoulderPositionOffset;
                }
            }
        }
    }
}
*/