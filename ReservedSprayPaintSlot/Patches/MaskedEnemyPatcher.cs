using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore.Patches;
using ReservedSprayPaintSlot.Patches;
using UnityEngine;
using Unity.Netcode;

/*
namespace ReservedSprayPaintSlot.Patches
{
    [HarmonyPatch]
    public class MaskedEnemyPatcher
    {
        public static Dictionary<MaskedPlayerEnemy, GameObject> heldSprayPaintByEnemy = new Dictionary<MaskedPlayerEnemy, GameObject>();
        public static HashSet<MaskedPlayerEnemy> spawnedEnemies = new HashSet<MaskedPlayerEnemy>();


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "OnDestroy")]
        [HarmonyPrefix]
        public static void OnDestroy(MaskedPlayerEnemy __instance)
        {
            if (heldSprayPaintByEnemy.TryGetValue(__instance, out var sprayPaintObject) && __instance.isEnemyDead)
            {
                Plugin.LogWarning("Destroying spray paint. Enemy destroyed.");
                GameObject.DestroyImmediate(sprayPaintObject);
                spawnedEnemies.Remove(__instance);
                heldSprayPaintByEnemy.Remove(__instance);
            }
        }


        [HarmonyPatch(typeof(MaskedPlayerEnemy), "LateUpdate")]
        [HarmonyPostfix]
        public static void ShowSprayPaintOnEnemy(MaskedPlayerEnemy __instance)
        {
            if (!spawnedEnemies.Contains(__instance) && SyncManager.syncReservedItemsList.Contains(Plugin.sprayPaintInfo) && !heldSprayPaintByEnemy.ContainsKey(__instance) && __instance.mimickingPlayer != null && PlayerPatcher.allPlayerData.TryGetValue(__instance.mimickingPlayer, out var playerData))
            {
                spawnedEnemies.Add(__instance);
                var sprayPaint = SprayPaintPatcher.GetReservedSprayPaint(playerData.playerController);
                if (sprayPaint != null)
                {
                    Plugin.LogWarning("OnMaskedEnemySpawn - MimickingPlayer: " + playerData.playerController.name + " - Spawning spray paint object on enemy.");

                    GameObject newSprayPaintObject = new GameObject("ReservedSprayPaint [MaskedEnemy]");
                    var lights = sprayPaint.GetComponentsInChildren<Light>();
                    var meshRenderer = sprayPaint.mainObjectRenderer;
                    foreach (var light in lights)
                    {
                        var newLight = GameObject.Instantiate(light.gameObject, light.transform.localPosition, light.transform.localRotation, newSprayPaintObject.transform).GetComponent<Light>();
                        newLight.enabled = true;
                        newLight.gameObject.layer = 6;
                    }
                    var newMeshRenderer = GameObject.Instantiate(meshRenderer.gameObject, meshRenderer.transform.localPosition, meshRenderer.transform.localRotation, newSprayPaintObject.transform).GetComponent<MeshRenderer>();
                    newMeshRenderer.enabled = true;
                    newMeshRenderer.gameObject.layer = 6;
                    newSprayPaintObject.transform.localScale = sprayPaint.transform.localScale;

                    heldSprayPaintByEnemy.Add(__instance, newSprayPaintObject);
                }
            }

            if (heldSprayPaintByEnemy.TryGetValue(__instance, out var sprayPaintObject))
            {
                if (__instance.isEnemyDead)
                {
                    Plugin.LogWarning("Destroying spray paint. Enemy dead.");
                    GameObject.DestroyImmediate(sprayPaintObject);
                    spawnedEnemies.Remove(__instance);
                    heldSprayPaintByEnemy.Remove(__instance);
                }
                else
                {
                    Transform parent = __instance.eye.parent.parent.parent.parent;
                    sprayPaintObject.transform.rotation = parent.rotation * Quaternion.Euler(SprayPaintPatcher.playerWaistRotationOffset);
                    sprayPaintObject.transform.position = parent.position + parent.rotation * SprayPaintPatcher.playerWaistPositionOffset;
                }
            }
        }
    }
}
*/