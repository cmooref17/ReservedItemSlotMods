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
		public static InputAction RawScrollAction;
		public static bool holdingModifierKey = false;
		public static bool scrollingReservedHotbar = false;


        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPrefix]
        private static void AddToKeybindMenu()
        {
            if (InputUtilsCompat.Enabled)
            {
                Asset = InputUtilsCompat.Asset;
                FocusReservedHotbarAction = InputUtilsCompat.FocusReservedHotbarHotkey;
            }
            else
            {
                Asset = ScriptableObject.CreateInstance<InputActionAsset>();
                ActionMap = new InputActionMap("ReservedItemSlots");
                Asset.AddActionMap(ActionMap);

                FocusReservedHotbarAction = ActionMap.AddAction("ReservedItemSlots.FocusReservedHotbar", binding: ConfigSettings.focusReservedHotbarHotkey.Value);
                FocusReservedHotbarAction.AddBinding("<Gamepad>/leftShoulder");
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
			if (!ConfigSettings.toggleFocusReservedHotbar.Value)
				FocusReservedHotbarAction.canceled += UnfocusReservedHotbarSlotsPerformed;
			RawScrollAction.performed += OnScrollReservedHotbar;
		}


		[HarmonyPatch(typeof(StartOfRound), "OnDisable")]
		[HarmonyPrefix]
        private static void OnDisable()
		{
			Asset.Disable();
            RawScrollAction.Disable();

            FocusReservedHotbarAction.performed -= FocusReservedHotbarSlotsAction;
            if (!ConfigSettings.toggleFocusReservedHotbar.Value)
                FocusReservedHotbarAction.canceled -= UnfocusReservedHotbarSlotsPerformed;
			RawScrollAction.performed -= OnScrollReservedHotbar;
		}


        private static void FocusReservedHotbarSlotsAction(InputAction.CallbackContext context)
		{
            if (localPlayerController == null || !localPlayerController.IsOwner || !localPlayerController.isPlayerControlled || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
				return;
			if (SessionManager.numReservedItemSlotsUnlocked <= 0 || !HUDPatcher.hasReservedItemSlotsAndEnabled)
				return;

            if (!ConfigSettings.toggleFocusReservedHotbar.Value)
				holdingModifierKey = true;
            if (!context.performed || !ReservedHotbarManager.CanSwapHotbars())
                return;

            if (!ConfigSettings.toggleFocusReservedHotbar.Value)
				ReservedHotbarManager.FocusReservedHotbarSlots(true);
			else
                ReservedHotbarManager.FocusReservedHotbarSlots(!ReservedPlayerData.localPlayerData.currentItemSlotIsReserved);
		}


        private static void UnfocusReservedHotbarSlotsPerformed(InputAction.CallbackContext context)
		{
			if (localPlayerController == null || !localPlayerController.IsOwner || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
				return;
			holdingModifierKey = false;
			
			if (!context.canceled || !ReservedHotbarManager.CanSwapHotbars())
				return;

            ReservedHotbarManager.FocusReservedHotbarSlots(false);
		}


		private static void OnScrollReservedHotbar(InputAction.CallbackContext context)
		{
			if (localPlayerController == null || !localPlayerController.IsOwner || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
				return;
			if (!context.performed || localPlayerController.inTerminalMenu || ReservedPlayerData.localPlayerData.throwingObject || scrollingReservedHotbar  || !ReservedPlayerData.localPlayerData.currentItemSlotIsReserved || ReservedPlayerData.localPlayerData.grabbingReservedItemData != null)
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