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
using ReservedFlashlightSlot.Config;


namespace ReservedFlashlightSlot.Patches
{
	[HarmonyPatch]
	public static class FlashlightPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static PlayerControllerB GetPreviousPlayerHeldBy(FlashlightItem flashlightItem) => (PlayerControllerB)Traverse.Create(flashlightItem).Field("previousPlayerHeldBy").GetValue();

        public static FlashlightItem GetMainFlashlight(PlayerControllerB playerController) => GetCurrentlySelectedFlashlight(playerController) ?? GetReservedFlashlight(playerController);
        public static FlashlightItem GetReservedFlashlight(PlayerControllerB playerController) => SyncManager.unlockableReservedItemSlotsDict.TryGetValue(Plugin.flashlightSlotData.slotName, out var reservedItemSlot) && reservedItemSlot.slotUnlocked && ReservedPlayerData.allPlayerData.TryGetValue(playerController, out var playerData) ? playerData.GetReservedItem(reservedItemSlot) as FlashlightItem : null;
        public static FlashlightItem GetCurrentlySelectedFlashlight(PlayerControllerB playerController) => playerController.currentItemSlot >= 0 && playerController.currentItemSlot < playerController.ItemSlots.Length ? playerController?.ItemSlots[playerController.currentItemSlot] as FlashlightItem : null;

        public static bool IsFlashlightOn(PlayerControllerB playerController) => GetMainFlashlight(playerController)?.isBeingUsed ?? false;

        
        [HarmonyPatch(typeof(FlashlightItem), "SwitchFlashlight")]
        [HarmonyPostfix]
        public static void OnSwitchOnOffFlashlight(bool on, FlashlightItem __instance)
        {
            if (__instance.playerHeldBy == null)
                return;

            //FlashlightItem flashlightItem = GetMainFlashlight(__instance.playerHeldBy);
            UpdateAllFlashlightStates(__instance.playerHeldBy, on);
        }


        [HarmonyPatch(typeof(FlashlightItem), "PocketItem")]
        [HarmonyPostfix]
        public static void OnPocketFlashlightLocal(FlashlightItem __instance)
        {
            OnPocketFlashlight(__instance, __instance.isBeingUsed);
        }


        // I don't like this method
        [HarmonyPatch(typeof(FlashlightItem), "PocketFlashlightClientRpc")]
        [HarmonyPrefix]
        public static void OnPocketFlashlightClientRpc(bool stillUsingFlashlight, FlashlightItem __instance)
        {
            if (!NetworkHelper.IsValidClientRpcExecStage(__instance))
                return;
            if (__instance.IsOwner || __instance.playerHeldBy == null || __instance.playerHeldBy == localPlayerController)
                return;
            OnPocketFlashlight(__instance, stillUsingFlashlight);
        }
        

        static void OnPocketFlashlight(FlashlightItem flashlightItem, bool stillUsingFlashlight = false)
        {
            if (flashlightItem.playerHeldBy == null)
                return;

            var currentlySelectedFlashlight = GetCurrentlySelectedFlashlight(flashlightItem.playerHeldBy);
            var reservedFlashlight = GetReservedFlashlight(flashlightItem.playerHeldBy);
            bool isBeingUsed = stillUsingFlashlight || (currentlySelectedFlashlight != null && currentlySelectedFlashlight.isBeingUsed);
            if (currentlySelectedFlashlight != null && currentlySelectedFlashlight.isBeingUsed)
                flashlightItem.playerHeldBy.pocketedFlashlight = null;
            else if (flashlightItem.isBeingUsed)
                flashlightItem.playerHeldBy.pocketedFlashlight = flashlightItem;
            else if (reservedFlashlight != null && (flashlightItem.playerHeldBy.pocketedFlashlight == null || !flashlightItem.playerHeldBy.pocketedFlashlight.isBeingUsed))
                flashlightItem.playerHeldBy.pocketedFlashlight = reservedFlashlight;
        }
        

        [HarmonyPatch(typeof(FlashlightItem), "EquipItem")]
        [HarmonyPostfix]
        public static void OnEquipFlashlight(FlashlightItem __instance)
        {
            if (__instance.playerHeldBy == null)
                return;

            bool isBeingUsed = (__instance.playerHeldBy.pocketedFlashlight != null && __instance.playerHeldBy.pocketedFlashlight.isBeingUsed) || __instance.isBeingUsed;

            var reservedFlashlight = GetReservedFlashlight(__instance.playerHeldBy);
            if (__instance.isBeingUsed || __instance == __instance.playerHeldBy.pocketedFlashlight)
                __instance.playerHeldBy.pocketedFlashlight = null;
            else if (reservedFlashlight != null && (__instance.playerHeldBy.pocketedFlashlight == null || !__instance.playerHeldBy.pocketedFlashlight.isBeingUsed))
                __instance.playerHeldBy.pocketedFlashlight = reservedFlashlight;

            UpdateAllFlashlightStates(__instance.playerHeldBy, isBeingUsed);
        }


        [HarmonyPatch(typeof(FlashlightItem), "DiscardItem")]
        [HarmonyPrefix]
        public static void ResetPocketedFlashlight(FlashlightItem __instance)
        {
            PlayerControllerB previouslyHeldBy = GetPreviousPlayerHeldBy(__instance);
            if (previouslyHeldBy == null)
                return;

            var reservedFlashlight = GetReservedFlashlight(previouslyHeldBy);
            if (reservedFlashlight != null && (__instance == previouslyHeldBy.pocketedFlashlight || previouslyHeldBy.pocketedFlashlight == null))
                previouslyHeldBy.pocketedFlashlight = reservedFlashlight;
        }


        static void UpdateAllFlashlightStates(PlayerControllerB playerController, bool mainFlashlightActive = true)
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


        static void UpdateFlashlightState(FlashlightItem flashlightItem, bool active)
        {
            if (flashlightItem.playerHeldBy == null)
                return;

            PlayerControllerB heldByPlayer = flashlightItem.playerHeldBy;

            flashlightItem.isBeingUsed = active;

            bool useFlashlightLight = heldByPlayer != localPlayerController || heldByPlayer.ItemSlots[heldByPlayer.currentItemSlot] == flashlightItem;
            flashlightItem.flashlightBulb.enabled = active && useFlashlightLight;
            flashlightItem.flashlightBulbGlow.enabled = active && useFlashlightLight;
            flashlightItem.usingPlayerHelmetLight = active && !useFlashlightLight;
        }
    }
}