using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using GameNetcodeStuff;
using UnityEngine.InputSystem;
using ReservedItemSlotCore.Patches;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore;
using ReservedItemSlotCore.Input;
using UnityEngine;
using ReservedItemSlotCore.Data;


namespace ReservedWeaponSlot.Input
{
	[HarmonyPatch]
	internal static class Keybinds
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static ReservedPlayerData localPlayerData { get { return ReservedPlayerData.localPlayerData; } }

        public static InputActionAsset Asset;
        public static InputActionMap ActionMap;

        static InputAction ToggleWeaponSlotAction;
        public static bool holdingWeaponModifier = false;
        public static bool toggledWeaponSlot = false;


        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPrefix]
        public static void AddToKeybindMenu()
        {
            Plugin.Log("Initializing hotkeys.");
            if (InputUtilsCompat.Enabled)
            {
                Asset = InputUtilsCompat.Asset;
                ActionMap = Asset.actionMaps[0];
                ToggleWeaponSlotAction = InputUtilsCompat.ToggleWeaponHotkey;
            }
            else
            {
                Asset = ScriptableObject.CreateInstance<InputActionAsset>();
                ActionMap = new InputActionMap("ReservedItemSlots");
                Asset.AddActionMap(ActionMap);

                ToggleWeaponSlotAction = ActionMap.AddAction("ReservedItemSlots.ToggleWeaponSlot", binding: "");
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "OnEnable")]
        [HarmonyPostfix]
        public static void OnEnable()
        {
            Asset.Enable();
            ToggleWeaponSlotAction.performed += OnSwapToWeaponSlot;
            ToggleWeaponSlotAction.canceled += OnSwapToWeaponSlot;
        }


        [HarmonyPatch(typeof(StartOfRound), "OnDisable")]
        [HarmonyPostfix]
        public static void OnDisable()
        {
            Asset.Disable();
            ToggleWeaponSlotAction.performed -= OnSwapToWeaponSlot;
            ToggleWeaponSlotAction.canceled -= OnSwapToWeaponSlot;
        }




        private static void OnSwapToWeaponSlot(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || localPlayerData == null || !localPlayerController.isPlayerControlled || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
                return;

            if (SessionManager.unlockedReservedItemSlotsDict.TryGetValue(Plugin.weaponSlotData.slotName, out var weaponSlotData))
            {
                List<ReservedItemSlotData> focusReservedItemSlots = new List<ReservedItemSlotData>() { weaponSlotData };
                if (SessionManager.unlockedReservedItemSlotsDict.TryGetValue(Plugin.rangedWeaponSlotData.slotName, out var rangedWeaponSlotData) && !focusReservedItemSlots.Contains(rangedWeaponSlotData))
                    focusReservedItemSlots.Add(rangedWeaponSlotData);

                ReservedHotbarManager.ForceToggleReservedHotbar(focusReservedItemSlots.ToArray());
                /*int inventoryIndex = ReservedPlayerData.localPlayerData.reservedHotbarStartIndex + weaponSlotData.GetReservedItemSlotIndex();
                if (inventoryIndex >= ReservedPlayerData.localPlayerData.reservedHotbarStartIndex && inventoryIndex < ReservedPlayerData.localPlayerData.reservedHotbarEndIndexExcluded && localPlayerController.ItemSlots[inventoryIndex] != null)
                    ReservedHotbarManager.ForceToggleReservedHotbar(inventoryIndex);*/
            }
            /*
                return;

            if (!ConfigSettings.swapToWeaponSlotToggled.Value)
            {
                holdingWeaponModifier = context.performed;
                if (localPlayerData.currentItemSlot != Patcher.reservedWeaponSlotIndex)
                {
                    // Swap to slot
                }
            }
            else if (context.performed)
            {
                toggledWeaponSlot = !toggledWeaponSlot;
                if (toggledWeaponSlot && localPlayerData.currentItemSlot != Patcher.reservedWeaponSlotIndex)
                {
                    
                }
                else if (!toggledWeaponSlot && localPlayerData.currentItemSlot == Patcher.reservedWeaponSlotIndex)
                {
                    
                }
            }
            */
        }
    }
}