using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using GameNetcodeStuff;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine;
using ReservedItemSlotCore.Patches;
using ReservedItemSlotCore.Config;
using ReservedItemSlotCore.Data;


namespace ReservedItemSlotCore.Input
{
	[HarmonyPatch]
	internal static class Keybinds
	{
		public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

		public static InputActionAsset Asset;
		public static InputActionMap ActionMap;

		public static InputAction FocusReservedHotbarAction;
		public static InputAction ToggleFocusReservedHotbarAction;
		public static InputAction RawScrollAction;

		public static bool holdingModifierKey = false;
		public static bool pressedToggleKey = false;
		public static bool scrollingReservedHotbar = false;


        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPrefix]
        private static void AddToKeybindMenu()
        {
            if (InputUtilsCompat.Enabled)
            {
                Asset = InputUtilsCompat.Asset;
                FocusReservedHotbarAction = InputUtilsCompat.FocusReservedHotbarHotkey;
                ToggleFocusReservedHotbarAction = InputUtilsCompat.ToggleFocusReservedHotbarHotkey;
            }
            else
            {
                Asset = ScriptableObject.CreateInstance<InputActionAsset>();
                ActionMap = new InputActionMap("ReservedItemSlots");
                Asset.AddActionMap(ActionMap);

                FocusReservedHotbarAction = ActionMap.AddAction("ReservedItemSlots.FocusReservedHotbar", binding: "<Keyboard>/leftAlt");
                FocusReservedHotbarAction.AddBinding("<Gamepad>/leftShoulder");

                ToggleFocusReservedHotbarAction = ActionMap.AddAction("ReservedItemSlots.ToggleFocusReservedHotbar", binding: "<Keyboard>/rightAlt");
                ToggleFocusReservedHotbarAction.AddBinding("<Gamepad>/leftShoulder");
            }

            RawScrollAction = new InputAction("ReservedItemSlots.RawScroll", binding: "<Mouse>/scroll/y");
        }


        [HarmonyPatch(typeof(StartOfRound), "OnEnable")]
		[HarmonyPrefix]
        private static void OnEnable()
		{
            holdingModifierKey = false;

			Asset.Enable();
			RawScrollAction.Enable();

            FocusReservedHotbarAction.performed += FocusReservedHotbarSlotsAction;
			FocusReservedHotbarAction.canceled += UnfocusReservedHotbarSlotsPerformed;
            ToggleFocusReservedHotbarAction.performed += ToggleFocusReservedHotbarSlotsAction;

			RawScrollAction.performed += OnScrollReservedHotbar;
		}


		[HarmonyPatch(typeof(StartOfRound), "OnDisable")]
		[HarmonyPrefix]
        private static void OnDisable()
		{
			Asset.Disable();
            RawScrollAction.Disable();

            FocusReservedHotbarAction.performed -= FocusReservedHotbarSlotsAction;
            FocusReservedHotbarAction.canceled -= UnfocusReservedHotbarSlotsPerformed;
            ToggleFocusReservedHotbarAction.performed -= ToggleFocusReservedHotbarSlotsAction;

			RawScrollAction.performed -= OnScrollReservedHotbar;
		}


        private static void FocusReservedHotbarSlotsAction(InputAction.CallbackContext context)
		{
            if (localPlayerController == null || !localPlayerController.IsOwner || !localPlayerController.isPlayerControlled || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
                return;
            // If the player has no unlocked reserved item slots
            if (SessionManager.numReservedItemSlotsUnlocked <= 0 || !HUDPatcher.hasReservedItemSlotsAndEnabled)
                return;
            // If the player has no held reserved item slots
            if (ReservedPlayerData.localPlayerData.GetNumHeldReservedItems() <= 0 && ConfigSettings.hideEmptyReservedItemSlots.Value)
			{
				if (ReservedPlayerData.localPlayerData.currentItemSlotIsReserved)
                    ReservedHotbarManager.FocusReservedHotbarSlots(false);
				return;
			}

            holdingModifierKey = true;
			pressedToggleKey = false;

            if (context.performed && ReservedHotbarManager.CanSwapHotbars())
			    ReservedHotbarManager.FocusReservedHotbarSlots(true);
		}


        private static void UnfocusReservedHotbarSlotsPerformed(InputAction.CallbackContext context)
		{
			if (localPlayerController == null || !localPlayerController.IsOwner || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
				return;

			holdingModifierKey = false;
            pressedToggleKey = false;


            if (context.performed && ReservedHotbarManager.CanSwapHotbars())
			    ReservedHotbarManager.FocusReservedHotbarSlots(false);
		}


        private static void ToggleFocusReservedHotbarSlotsAction(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !localPlayerController.IsOwner || !localPlayerController.isPlayerControlled || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
                return;
			// Don't toggle if we're currently holding to focus
			if (ReservedPlayerData.localPlayerData.currentItemSlotIsReserved && holdingModifierKey)
				return;
			// If the player has no unlocked reserved item slots
            if (SessionManager.numReservedItemSlotsUnlocked <= 0 || !HUDPatcher.hasReservedItemSlotsAndEnabled)
                return;
			// If the player has no held reserved item slots
            if (ReservedPlayerData.localPlayerData.GetNumHeldReservedItems() <= 0 && ConfigSettings.hideEmptyReservedItemSlots.Value)
            {
                if (ReservedPlayerData.localPlayerData.currentItemSlotIsReserved)
                    ReservedHotbarManager.FocusReservedHotbarSlots(false);
                return;
            }

			// If keybind is the same as hold to focus, return
            int bindingIndex = StartOfRound.Instance.localPlayerUsingController ? 1 : 0;
			if (FocusReservedHotbarAction.bindings[bindingIndex].effectivePath == ToggleFocusReservedHotbarAction.bindings[bindingIndex].effectivePath)
				return;

			holdingModifierKey = false;
			pressedToggleKey = true;

            if (context.performed && ReservedHotbarManager.CanSwapHotbars())
                ReservedHotbarManager.FocusReservedHotbarSlots(!ReservedPlayerData.localPlayerData.currentItemSlotIsReserved);
        }


        private static void OnScrollReservedHotbar(InputAction.CallbackContext context)
		{
			if (localPlayerController == null || !localPlayerController.IsOwner || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
				return;
			if (!context.performed || localPlayerController.inTerminalMenu || ReservedPlayerData.localPlayerData.throwingObject || scrollingReservedHotbar || !ReservedPlayerData.localPlayerData.currentItemSlotIsReserved || ReservedPlayerData.localPlayerData.grabbingReservedItemData != null)
				return;

			IEnumerator ResetScrollDelayed()
			{
				yield return null;
				yield return new WaitForEndOfFrame();
				scrollingReservedHotbar = false;
			}

			scrollingReservedHotbar = true;
			MethodInfo method = localPlayerController.GetType().GetMethod("ScrollMouse_performed", BindingFlags.NonPublic | BindingFlags.Instance);
			method.Invoke(localPlayerController, new object[] { context });
			localPlayerController.StartCoroutine(ResetScrollDelayed());
		}
	}
}