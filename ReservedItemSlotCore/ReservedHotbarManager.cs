using GameNetcodeStuff;
using HarmonyLib;
using ReservedItemSlotCore.Config;
using ReservedItemSlotCore.Data;
using ReservedItemSlotCore.Input;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Diagnostics;


namespace ReservedItemSlotCore
{
    [HarmonyPatch]
    public static class ReservedHotbarManager
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static ReservedPlayerData localPlayerData { get { return ReservedPlayerData.localPlayerData; } }

        static int vanillaHotbarSize = -1;

        public static int reservedHotbarSize { get { return SessionManager.numReservedItemSlotsUnlocked; } }
        public static int indexInHotbar = 0;
        public static int indexInReservedHotbar = 0;

        public static bool isToggledInReservedSlots { get { var currentlySelectedReservedItemSlot = localPlayerData.GetCurrentlySelectedReservedItemSlot(); return ConfigSettings.toggleFocusReservedHotbar.Value || (currentlyToggledItemSlots != null && currentlySelectedReservedItemSlot != null && currentlyToggledItemSlots.Contains(currentlySelectedReservedItemSlot)); } }
        internal static List<ReservedItemSlotData> currentlyToggledItemSlots = new List<ReservedItemSlotData>();


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void InitSession(StartOfRound __instance)
        {
            currentlyToggledItemSlots = new List<ReservedItemSlotData>();
            ReservedPlayerData.allPlayerData.Clear();
            vanillaHotbarSize = -1;
            indexInHotbar = 0;
            indexInReservedHotbar = -1;
        }


        /// <summary>
        /// For force toggling a set of reserved item slots. These slots will be toggled without needing to hold Alt, and will remain toggled until pressed again, or scrolled off of the slot.
        /// </summary>
        /// <param name="reservedItemSlots"></param>
        public static void ForceToggleReservedHotbar(params ReservedItemSlotData[] reservedItemSlots)
        {
            if (!localPlayerController.IsOwner || !localPlayerController.isPlayerControlled || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
                return;
            if (!HUDPatcher.hasReservedItemSlotsAndEnabled || reservedHotbarSize <= 0 || !CanSwapHotbars() || reservedItemSlots == null || reservedItemSlots.Length <= 0 || localPlayerController == null)
                return;

            //if (!ConfigSettings.toggleFocusReservedHotbar.Value)

            currentlyToggledItemSlots = new List<ReservedItemSlotData>(reservedItemSlots);
            var firstSlot = currentlyToggledItemSlots.First().GetReservedItemSlotIndex() + localPlayerData.reservedHotbarStartIndex;
            bool isReservedSlot = ReservedPlayerData.localPlayerData.IsReservedItemSlot(firstSlot);

            if (currentlyToggledItemSlots.Contains(localPlayerData.GetCurrentlySelectedReservedItemSlot()))
                FocusReservedHotbarSlots(false);
            else
            {
                HUDPatcher.UpdateToggledReservedItemSlotsUI();
                FocusReservedHotbarSlots(isReservedSlot, firstSlot);
            }
        }


        /// <summary>
        /// Focuses or unfocuses the reserved hotbar slots.
        /// </summary>
        /// <param name="active">True = Focus</param>
        /// <param name="forceSlot">ForceSwapToInventoryIndex (optional)</param>
        public static void FocusReservedHotbarSlots(bool active, int forceSlot = -1)
        {
            if (!HUDPatcher.hasReservedItemSlotsAndEnabled)
                return;
            if (reservedHotbarSize <= 0 && active)
                return;
            if (ReservedPlayerData.localPlayerData.currentItemSlotIsReserved == active && (forceSlot == -1 || localPlayerData.currentItemSlot == forceSlot))
                return;

            if (forceSlot != -1)
                active = localPlayerData.IsReservedItemSlot(forceSlot);

            var playerData = ReservedPlayerData.localPlayerData;

            indexInHotbar = Mathf.Clamp(indexInHotbar, 0, localPlayerController.ItemSlots.Length - 1);
            indexInHotbar = playerData.IsReservedItemSlot(indexInHotbar) ? 0 : indexInHotbar;
            indexInReservedHotbar = Mathf.Clamp(indexInReservedHotbar, playerData.reservedHotbarStartIndex, playerData.reservedHotbarEndIndexExcluded - 1);

            int currentIndex = Mathf.Clamp(localPlayerController.currentItemSlot, 0, localPlayerController.ItemSlots.Length);
            int newIndex = currentIndex;
            bool inReservedItemSlots = active;

            // Was not in reserved, now in reserved
            if (inReservedItemSlots && (!playerData.IsReservedItemSlot(currentIndex) || forceSlot != -1))
            {
                indexInHotbar = currentIndex;
                indexInHotbar = playerData.IsReservedItemSlot(indexInHotbar) ? 0 : indexInHotbar;
                if (forceSlot != -1 && playerData.IsReservedItemSlot(forceSlot))
                    indexInReservedHotbar = forceSlot;
                newIndex = indexInReservedHotbar;

                if (localPlayerController.ItemSlots[newIndex] == null && playerData.GetNumHeldReservedItems() > 0)
                {
                    for (newIndex = playerData.reservedHotbarStartIndex; newIndex < playerData.reservedHotbarEndIndexExcluded; newIndex++)
                    {
                        if (localPlayerController.ItemSlots[newIndex] != null)
                            break;
                    }
                }
                Plugin.Log("Focusing reserved hotbar slots. NewIndex: " + newIndex + " OldIndex: " + currentIndex + " ReservedStartIndex: " + ReservedPlayerData.localPlayerData.reservedHotbarStartIndex);
            }

            // Was in reserved, now not in reserved
            else if (!inReservedItemSlots && (ReservedPlayerData.localPlayerData.IsReservedItemSlot(currentIndex) || forceSlot != -1))
            {
                indexInReservedHotbar = Mathf.Clamp(currentIndex, playerData.reservedHotbarStartIndex, playerData.reservedHotbarEndIndexExcluded - 1);
                if (forceSlot != -1 && !playerData.IsReservedItemSlot(forceSlot))
                    indexInHotbar = forceSlot;
                newIndex = indexInHotbar;
                Plugin.Log("Unfocusing reserved hotbar slots. NewIndex: " + newIndex + " OldIndex: " + currentIndex + " ReservedStartIndex: " + ReservedPlayerData.localPlayerData.reservedHotbarStartIndex);
            }

            if (newIndex < 0)
                Plugin.LogError("Swapping to hotbar slot: " + newIndex + ". Maybe send these logs to Flip? :)");
            else if (newIndex >= localPlayerController.ItemSlots.Length)
                Plugin.LogError("Swapping to hotbar slot: " + newIndex + " InventorySize: " + localPlayerController.ItemSlots.Length + ". Maybe send these logs to Flip? :)");

            SyncManager.SwapHotbarSlot(newIndex);
            if (localPlayerController.currentItemSlot != newIndex)
                Plugin.LogWarning("OnFocusReservedHotbarSlots - New hotbar index does not match target hotbar index. Tried to swap to index: " + newIndex + " Current index: " + localPlayerController.currentItemSlot + " Tried swapping to reserved hotbar: " + active);
        }


        public static bool CanSwapHotbars()
        {
            if (!HUDPatcher.hasReservedItemSlotsAndEnabled)
                return false;

            return !(ReservedPlayerData.localPlayerData.grabbingReservedItemData != null || localPlayerController.performingEmote || localPlayerController.isGrabbingObjectAnimation || localPlayerController.quickMenuManager.isMenuOpen || localPlayerController.inSpecialInteractAnimation || localPlayerData.throwingObject || localPlayerController.isTypingChat || localPlayerController.twoHanded || localPlayerController.activatingItem || localPlayerController.jetpackControls || localPlayerController.disablingJetpackControls || localPlayerController.inTerminalMenu || localPlayerController.isPlayerDead || localPlayerData.timeSinceSwitchingSlots < 0.3f);
        }


        internal static bool CanGrabReservedItem()
        {
            if (!HUDPatcher.hasReservedItemSlotsAndEnabled || localPlayerData.GetNumHeldReservedItems() == 0 || localPlayerData.isGrabbingReservedItem || (localPlayerData.inReservedHotbarSlots && !ConfigSettings.toggleFocusReservedHotbar.Value && !isToggledInReservedSlots))
                return false;

            return true;
        }


        internal static void OnSwapToReservedHotbar()
        {
            if (!localPlayerData.currentItemSlotIsReserved)
                return;

            var currentlySelectedReservedItemSlot = localPlayerData.GetCurrentlySelectedReservedItemSlot();
            if (isToggledInReservedSlots && currentlyToggledItemSlots != null && !currentlyToggledItemSlots.Contains(currentlySelectedReservedItemSlot))
                currentlyToggledItemSlots = null;
        }


        internal static void OnSwapToVanillaHotbar()
        {
            if (localPlayerData.currentItemSlotIsReserved)
                return;

            currentlyToggledItemSlots = null;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPrefix]
        private static void RefocusReservedHotbarAfterAnimation(PlayerControllerB __instance)
        {
            if (!HUDPatcher.hasReservedItemSlotsAndEnabled)
                return;
            if (__instance != localPlayerController || ConfigSettings.toggleFocusReservedHotbar.Value || Keybinds.holdingModifierKey == ReservedPlayerData.localPlayerData.currentItemSlotIsReserved || ReservedHotbarManager.isToggledInReservedSlots)
                return;

            if (CanSwapHotbars())
                FocusReservedHotbarSlots(Keybinds.holdingModifierKey);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "UpdateSpecialAnimationValue")]
        [HarmonyPostfix]
        private static void UpdateReservedHotbarAfterAnimation(bool specialAnimation, PlayerControllerB __instance)
        {
            if (!HUDPatcher.hasReservedItemSlotsAndEnabled)
                return;
            if (__instance != localPlayerController)
                return;

            if (!specialAnimation && !ConfigSettings.toggleFocusReservedHotbar.Value && ReservedPlayerData.localPlayerData.currentItemSlotIsReserved != Keybinds.holdingModifierKey)
                FocusReservedHotbarSlots(Keybinds.holdingModifierKey);
        }
    }
}
