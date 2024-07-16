using HarmonyLib;
using UnityEngine;
using GameNetcodeStuff;
using ReservedItemSlotCore.Patches;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore.Input;
using Unity.Netcode;
using System.Collections;
using UnityEngine.InputSystem;
using ReservedItemSlotCore;
using ReservedItemSlotCore.Data;
using System;
using System.Diagnostics.Eventing.Reader;

namespace ReservedFlashlightSlot.Patches
{
	[HarmonyPatch]
	public static class FlashlightPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static PlayerControllerB GetPreviousPlayerHeldBy(FlashlightItem flashlightItem) => (PlayerControllerB)Traverse.Create(flashlightItem).Field("previousPlayerHeldBy").GetValue();

        public static FlashlightItem GetMainFlashlight(PlayerControllerB playerController)
        {
            var currentlySelectedFlashlight = GetCurrentlySelectedFlashlight(playerController);
            if (currentlySelectedFlashlight)
                return currentlySelectedFlashlight;

            var pocketedFlashlight = playerController?.pocketedFlashlight as FlashlightItem;
            var reservedFlashlight = GetReservedFlashlight(playerController);
            return pocketedFlashlight && (!reservedFlashlight || (pocketedFlashlight.isBeingUsed && !reservedFlashlight.isBeingUsed)) ? pocketedFlashlight : reservedFlashlight;
        }
        public static FlashlightItem GetReservedFlashlight(PlayerControllerB playerController) => SessionManager.TryGetUnlockedItemSlotData(Plugin.flashlightSlotData.slotName, out var itemSlot) && ReservedPlayerData.allPlayerData.TryGetValue(playerController, out var playerData) ? playerData.GetReservedItem(itemSlot) as FlashlightItem : null;
        public static FlashlightItem GetCurrentlySelectedFlashlight(PlayerControllerB playerController) => playerController.currentItemSlot >= 0 && playerController.currentItemSlot < playerController.ItemSlots.Length ? playerController?.ItemSlots[playerController.currentItemSlot] as FlashlightItem : null;
        public static FlashlightItem GetFirstFlashlightItem(PlayerControllerB playerController) { foreach (var item in playerController.ItemSlots) { if (item != null && item is FlashlightItem flashlightItem) return flashlightItem; } return null; }




        [HarmonyPatch(typeof(FlashlightItem), "PocketItem")]
        [HarmonyPostfix]
        private static void OnPocketFlashlight(FlashlightItem __instance)
        {
            OnPocketFlashlightLocal(__instance);
        }


        [HarmonyPatch(typeof(FlashlightItem), "PocketFlashlightClientRpc")]
        [HarmonyPostfix]
        private static void OnPocketFlashlightClientRpc(bool stillUsingFlashlight, FlashlightItem __instance)
        {
            var playerHeldBy = __instance?.playerHeldBy;
            if (!playerHeldBy || playerHeldBy == localPlayerController)
                return;

            if (NetworkManager.Singleton.IsClient && (int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue() == 2)
                OnPocketFlashlightLocal(__instance);
        }


        private static void OnPocketFlashlightLocal(FlashlightItem flashlightItem)
        {
            var playerHeldBy = flashlightItem?.playerHeldBy;
            if (!playerHeldBy)
                return;

            var pocketedFlashlight = playerHeldBy.pocketedFlashlight as FlashlightItem;
            if (flashlightItem.isBeingUsed)
            {
                if (pocketedFlashlight && pocketedFlashlight != flashlightItem)
                    UpdateFlashlightState(pocketedFlashlight, false);
                UpdateFlashlightState(flashlightItem, true);
                playerHeldBy.pocketedFlashlight = flashlightItem;
            }
        }


        [HarmonyPatch(typeof(FlashlightItem), "EquipItem")]
        [HarmonyPostfix]
        private static void OnEquipFlashlight(FlashlightItem __instance)
        {
            var playerHeldBy = __instance?.playerHeldBy;
            if (!playerHeldBy)
                return;

            var pocketedFlashlight = playerHeldBy.pocketedFlashlight as FlashlightItem;
            if (__instance != pocketedFlashlight)
            {
                if (__instance.isBeingUsed)
                {
                    UpdateFlashlightState(__instance, true);
                    if (pocketedFlashlight)
                        UpdateFlashlightState(pocketedFlashlight, false);
                }
                else if (pocketedFlashlight && pocketedFlashlight.isBeingUsed)
                {
                    UpdateFlashlightState(__instance, false);
                    UpdateFlashlightState(pocketedFlashlight, true);
                }
            }
        }


        [HarmonyPatch(typeof(FlashlightItem), "DiscardItem")]
        [HarmonyPostfix]
        private static void ResetPocketedFlashlightPost(FlashlightItem __instance)
        {
            if (!__instance)
                return;

            var previouslyHeldBy = GetPreviousPlayerHeldBy(__instance);
            if (!previouslyHeldBy)
            {
                foreach (var playerController in StartOfRound.Instance.allPlayerScripts)
                {
                    if (__instance == playerController.pocketedFlashlight)
                    {
                        previouslyHeldBy = playerController;
                        break;
                    }
                }
                if (!previouslyHeldBy)
                    return;
            }

            var currentlySelectedFlashlight = GetCurrentlySelectedFlashlight(previouslyHeldBy);
            var reservedFlashlight = GetReservedFlashlight(previouslyHeldBy);
            var pocketedFlashlight = previouslyHeldBy.pocketedFlashlight as FlashlightItem;
            if (__instance == pocketedFlashlight)
            {
                var mainFlashlight = reservedFlashlight && (!currentlySelectedFlashlight || (reservedFlashlight.isBeingUsed && !currentlySelectedFlashlight.isBeingUsed)) ? reservedFlashlight : currentlySelectedFlashlight;
                previouslyHeldBy.pocketedFlashlight = null;
                if (mainFlashlight)
                {
                    if (mainFlashlight != currentlySelectedFlashlight)
                        previouslyHeldBy.pocketedFlashlight = mainFlashlight;
                    UpdateFlashlightState(mainFlashlight, mainFlashlight.isBeingUsed);
                }
            }
            else if (pocketedFlashlight != null && pocketedFlashlight.isBeingUsed)
                UpdateFlashlightState(pocketedFlashlight, true); // Should turn the player's helmet light back on
        }


        [HarmonyPatch(typeof(FlashlightItem), "SwitchFlashlight")]
        [HarmonyPostfix]
        private static void OnToggleFlashlight(bool on, FlashlightItem __instance)
        {
            var playerHeldBy = __instance?.playerHeldBy;
            if (!playerHeldBy)
                return;

            var pocketedFlashlight = playerHeldBy.pocketedFlashlight as FlashlightItem;
            if (!__instance.isBeingUsed)
            {
                if (__instance == pocketedFlashlight)
                    playerHeldBy.pocketedFlashlight = null;
                return;
            }
            else
                Traverse.Create(__instance).Field("previousPlayerHeldBy").SetValue(playerHeldBy);

            if (pocketedFlashlight != null && pocketedFlashlight != __instance)
            {
                if (pocketedFlashlight.isBeingUsed)
                {
                    pocketedFlashlight.SwitchFlashlight(false);
                    UpdateFlashlightState(pocketedFlashlight, false);
                }
                playerHeldBy.pocketedFlashlight = null;
            }

            if (__instance != GetCurrentlySelectedFlashlight(playerHeldBy))
                playerHeldBy.pocketedFlashlight = __instance;

            UpdateFlashlightState(__instance, true);
        }


        /*internal static void UpdateAllFlashlightStates(PlayerControllerB playerController, bool mainFlashlightActive = true)
        {
            FlashlightItem mainFlashlight = GetMainFlashlight(playerController);
            if (mainFlashlight == null)
            {
                playerController.helmetLight.enabled = false;
                mainFlashlightActive = false;
            }
            else
                playerController.ChangeHelmetLight(mainFlashlight.flashlightTypeID, mainFlashlightActive && playerController == localPlayerController && playerController.ItemSlots[playerController.currentItemSlot] != mainFlashlight);
            
            for (int i = 0; i < playerController.ItemSlots.Length; i++)
            {
                FlashlightItem flashlight = playerController.ItemSlots[i] as FlashlightItem;
                if (flashlight != null)
                    UpdateFlashlightState(flashlight, flashlight == mainFlashlight && mainFlashlightActive);
            }
        }*/


        internal static void UpdateFlashlightState(FlashlightItem flashlightItem, bool active)
        {
            var playerHeldBy = flashlightItem?.playerHeldBy;
            if (!playerHeldBy)
                return;

            flashlightItem.isBeingUsed = active;
            bool useFlashlightLight = active && (flashlightItem == GetCurrentlySelectedFlashlight(playerHeldBy) || (playerHeldBy != localPlayerController && flashlightItem == GetReservedFlashlight(playerHeldBy)));
            bool useHelmetLight = active && !useFlashlightLight;

            flashlightItem.flashlightBulb.enabled = useFlashlightLight;
            flashlightItem.flashlightBulbGlow.enabled = useFlashlightLight;
            flashlightItem.usingPlayerHelmetLight = useHelmetLight;
            playerHeldBy.helmetLight.enabled = useHelmetLight;
            if (useHelmetLight)
                playerHeldBy.ChangeHelmetLight(flashlightItem.flashlightTypeID);

            if (flashlightItem.changeMaterial)
            {
                try { flashlightItem.flashlightMesh.sharedMaterials[1] = useFlashlightLight ? flashlightItem.bulbLight : flashlightItem.bulbDark; }
                catch { }
            }
        }
    }
}