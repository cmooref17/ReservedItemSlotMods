﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using GameNetcodeStuff;
using UnityEngine.InputSystem;
using UnityEngine;
using ReservedItemSlotCore.Patches;
using ReservedFlashlightSlot.Patches;
using ReservedFlashlightSlot.Config;

namespace ReservedFlashlightSlot.Input
{
	[HarmonyPatch]
	internal static class Keybinds
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static InputActionAsset Asset;
        public static InputActionMap ActionMap;

        static InputAction ActivateFlashlightAction;


        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPrefix]
        public static void AddToKeybindMenu()
        {
            Plugin.Log("Initializing hotkeys.");
            if (InputUtilsCompat.Enabled)
            {
                Asset = InputUtilsCompat.Asset;
                ActionMap = Asset.actionMaps[0];
                ActivateFlashlightAction = InputUtilsCompat.ToggleFlashlightHotkey;
            }
            else
            {
                Asset = ScriptableObject.CreateInstance<InputActionAsset>();
                ActionMap = new InputActionMap("ReservedItemSlots");
                Asset.AddActionMap(ActionMap);

                ActivateFlashlightAction = ActionMap.AddAction("ReservedItemSlots.ToggleFlashlight", binding: "<keyboard>/f");
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "OnEnable")]
        [HarmonyPostfix]
        public static void OnEnable()
        {
            Asset.Enable();
            ActivateFlashlightAction.performed += OnActivateFlashlightPerformed;
        }


        [HarmonyPatch(typeof(StartOfRound), "OnDisable")]
        [HarmonyPostfix]
        public static void OnDisable()
        {
            Asset.Disable();
            ActivateFlashlightAction.performed -= OnActivateFlashlightPerformed;
        }





        private static void OnActivateFlashlightPerformed(InputAction.CallbackContext context)
        {
            if (localPlayerController == null || !localPlayerController.isPlayerControlled || (localPlayerController.IsServer && !localPlayerController.isHostPlayerObject))
                return;
            FlashlightItem mainFlashlight = FlashlightPatcher.GetMainFlashlight(localPlayerController);
            if (!context.performed || mainFlashlight == null || ShipBuildModeManager.Instance.InBuildMode || localPlayerController.inTerminalMenu)
                return;

            float timeSinceSwitchingSlots = (float)Traverse.Create(localPlayerController).Field("timeSinceSwitchingSlots").GetValue();
            if (timeSinceSwitchingSlots < 0.075f)
                return;

            mainFlashlight.UseItemOnClient(!mainFlashlight.isBeingUsed);
            Traverse.Create(localPlayerController).Field("timeSinceSwitchingSlots").SetValue(0);
        }
    }
}