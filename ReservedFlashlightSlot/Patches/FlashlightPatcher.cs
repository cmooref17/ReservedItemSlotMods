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


        [HarmonyPatch(typeof(FlashlightItem), "EquipItem")]
        [HarmonyPostfix]
        public static void OnEquipFlashlight(FlashlightItem __instance)
        {
            var heldByPlayer = __instance?.playerHeldBy;
            if (!heldByPlayer)
                return;

            var pocketedFlashlight = heldByPlayer.pocketedFlashlight as FlashlightItem;
            if (!__instance.isBeingUsed && pocketedFlashlight != null && pocketedFlashlight != __instance && pocketedFlashlight.isBeingUsed)
                UpdateFlashlightState(pocketedFlashlight, true);
        }


        [HarmonyPatch(typeof(FlashlightItem), "DiscardItem")]
        [HarmonyPostfix]
        public static void ResetPocketedFlashlightPost(FlashlightItem __instance)
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
        public static void OnToggleFlashlight(bool on, FlashlightItem __instance)
        {
            var heldByPlayer = __instance?.playerHeldBy;
            Plugin.LogWarning("HeldByPlayer? " + (heldByPlayer == null ? "NO" : "YES IsBeingUsed: " + __instance.isBeingUsed));
            if (!heldByPlayer/* || __instance == heldByPlayer.pocketedFlashlight*/)
                return;

            int indexInInventory = Array.IndexOf(heldByPlayer.ItemSlots, __instance);
            Plugin.LogWarning("Switching flashlight at index: " + indexInInventory + " On: " + on + " IsBeingUsed: " + __instance.isBeingUsed);

            var pocketedFlashlight = heldByPlayer.pocketedFlashlight as FlashlightItem;
            if (!__instance.isBeingUsed)
            {
                if (__instance == pocketedFlashlight)
                    heldByPlayer.pocketedFlashlight = null;
                return;
            }
            else
                Traverse.Create(__instance).Field("previousPlayerHeldBy").SetValue(heldByPlayer);

            Plugin.LogWarning("NotCurrentlySelectedFlashlight");
            if (pocketedFlashlight != null && pocketedFlashlight != __instance)
            {
                if (pocketedFlashlight.isBeingUsed)
                {
                    Plugin.LogWarning("Deactivate Pocketed Flashlight");
                    pocketedFlashlight.SwitchFlashlight(false);
                    UpdateFlashlightState(pocketedFlashlight, false);
                }
                heldByPlayer.pocketedFlashlight = null;
            }

            if (__instance != GetCurrentlySelectedFlashlight(heldByPlayer))
            {
                Plugin.LogWarning("Activate Flashlight");
                heldByPlayer.pocketedFlashlight = __instance;
                UpdateFlashlightState(__instance, true);
            }

            //UpdateAllFlashlightStates(heldByPlayer, true);
            /*{
                if (__instance.playerHeldBy != null && __instance != __instance.playerHeldBy.ItemSlots[__instance.playerHeldBy.currentItemSlot])
                {
                    if (__instance.playerHeldBy.pocketedFlashlight != null && __instance.playerHeldBy.pocketedFlashlight != __instance && __instance.playerHeldBy.pocketedFlashlight.isBeingUsed)
                        ((FlashlightItem)__instance.playerHeldBy.pocketedFlashlight).SwitchFlashlight(false);
                    __instance.playerHeldBy.pocketedFlashlight = __instance;
                }
            }*/
        }


        internal static void UpdateAllFlashlightStates(PlayerControllerB playerController, bool mainFlashlightActive = true)
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
        }


        internal static void UpdateFlashlightState(FlashlightItem flashlightItem, bool active)
        {
            var heldByPlayer = flashlightItem?.playerHeldBy;
            if (!heldByPlayer)
                return;

            flashlightItem.isBeingUsed = active;
            bool useFlashlightLight = heldByPlayer != localPlayerController || flashlightItem == GetCurrentlySelectedFlashlight(heldByPlayer);

            flashlightItem.flashlightBulb.enabled = active && useFlashlightLight;
            flashlightItem.flashlightBulbGlow.enabled = active && useFlashlightLight;
            flashlightItem.usingPlayerHelmetLight = active && !useFlashlightLight;
            heldByPlayer.helmetLight.enabled = active && !useFlashlightLight;
            if (heldByPlayer.helmetLight.enabled)
                heldByPlayer.ChangeHelmetLight(flashlightItem.flashlightTypeID);
        }
    }
}