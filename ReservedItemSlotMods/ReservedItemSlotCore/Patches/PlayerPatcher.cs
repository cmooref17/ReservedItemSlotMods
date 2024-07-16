using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using HarmonyLib;
using GameNetcodeStuff;
using TMPro;
using System.Collections;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore.Config;
using ReservedItemSlotCore.Input;
using ReservedItemSlotCore.Compatibility;
using ReservedItemSlotCore.Data;
using System.Data.SqlTypes;
using System.Linq;
using UnityEngine.PlayerLoop;

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    internal static class PlayerPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static Dictionary<PlayerControllerB, ReservedPlayerData> allPlayerData { get { return ReservedPlayerData.allPlayerData; } }
        public static ReservedPlayerData localPlayerData { get { return ReservedPlayerData.localPlayerData; }  }

        private static int INTERACTABLE_OBJECT_MASK = 0;
        public static int vanillaHotbarSize = 4;
        public static int reservedHotbarSize { get { return SessionManager.numReservedItemSlotsUnlocked; } }

        private static bool initialized = false;


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        private static void InitSession(StartOfRound __instance)
        {
            initialized = false;
            vanillaHotbarSize = 4;
            ReservedPlayerData.allPlayerData?.Clear();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Awake")]
        [HarmonyPostfix]
        private static void InitializePlayerController(PlayerControllerB __instance)
        {
            if (!initialized)
            {
                vanillaHotbarSize = __instance.ItemSlots.Length;
                INTERACTABLE_OBJECT_MASK = (int)Traverse.Create(__instance).Field("interactableObjectsMask").GetValue();
                initialized = true;
            }
        }

        
        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPrefix]
        private static void InitializePlayerControllerLate(PlayerControllerB __instance)
        {
            var playerData = new ReservedPlayerData(__instance);
            if (!allPlayerData.ContainsKey(__instance))
            {
                Plugin.Log("Initializing ReservedPlayerData for player: " + __instance.name);
                allPlayerData.Add(__instance, playerData);
            }
        }
        

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        private static void CheckForChangedInventorySize(PlayerControllerB __instance)
        {
            if (!SyncManager.isSynced)
                return;

            if (!ReservedPlayerData.allPlayerData.TryGetValue(__instance, out var playerData))
                return;

            if (!SyncManager.canUseModDisabledOnHost || __instance != localPlayerController || reservedHotbarSize <= 0 || playerData.hotbarSize == __instance.ItemSlots.Length)
                return;

            Plugin.LogWarning("On update inventory size for player: " + __instance.name + " - Old hotbar size: " + playerData.hotbarSize + " - New hotbar size: " + __instance.ItemSlots.Length);
            playerData.hotbarSize = __instance.ItemSlots.Length;

            int startIndex = -1;
            // Try to calculate start reserved hotbarbar index
            // Easiest (if local player)
            if (__instance == localPlayerController)
            {
                if (HUDPatcher.reservedItemSlots != null && HUDPatcher.reservedItemSlots.Count > 0)
                {
                    startIndex = Array.IndexOf(HUDManager.Instance.itemSlotIconFrames, HUDPatcher.reservedItemSlots[0]);
                    Plugin.Log("OnUpdateInventorySize A for local player: " + __instance.name + " NewReservedItemsStartIndex: " + startIndex);
                }

                if (startIndex == -1)
                {
                    for (int newStartIndex = 0; newStartIndex < HUDManager.Instance.itemSlotIconFrames.Length; newStartIndex++)
                    {
                        if (HUDManager.Instance.itemSlotIconFrames[newStartIndex].name.ToLower().Contains("reserved"))
                        {
                            startIndex = newStartIndex;
                            Plugin.Log("OnUpdateInventorySize B for local player: " + __instance.name + " NewReservedItemsStartIndex: " + startIndex);
                            break;
                        }
                    }
                }
            }

            // Try setting to previous start (unchanged?)
            if (startIndex == -1)
            {
                startIndex = playerData.reservedHotbarStartIndex;
                Plugin.Log("OnUpdateInventorySize C for player: " + __instance.name + " NewReservedItemsStartIndex: " + startIndex);
            }

            // Just in case
            if (startIndex == -1)
            {
                startIndex = vanillaHotbarSize;
                Plugin.Log("OnUpdateInventorySize D for player: " + __instance.name + " NewReservedItemsStartIndex: " + startIndex);
            }

            playerData.reservedHotbarStartIndex = startIndex;

            if (playerData.reservedHotbarStartIndex < 0)
                Plugin.LogError("Set new reserved start index to slot: " + playerData.reservedHotbarStartIndex + ". Maybe share these logs with Flip? :)");
            if (playerData.reservedHotbarEndIndexExcluded - 1 >= playerData.playerController.ItemSlots.Length)
                Plugin.LogError("Set new reserved start index to slot: " + playerData.reservedHotbarStartIndex + " Last reserved slot index: " + (playerData.reservedHotbarEndIndexExcluded - 1) + " Inventory size: " + playerData.playerController.ItemSlots.Length + ". Maybe share these logs with Flip? :)");

            if (playerData.isLocalPlayer)
                HUDPatcher.UpdateUI();
        }





        [HarmonyPatch(typeof(PlayerControllerB), "BeginGrabObject")]
        [HarmonyPrefix]
        private static bool BeginGrabReservedItemPrefix(PlayerControllerB __instance)
        {
            if ((!SyncManager.isSynced && !SyncManager.canUseModDisabledOnHost) || !HUDPatcher.hasReservedItemSlotsAndEnabled)
                return true;

            localPlayerData.grabbingReservedItemSlotData = null;
            localPlayerData.grabbingReservedItemData = null;
            localPlayerData.grabbingReservedItem = null;
            localPlayerData.previousHotbarIndex = -1;

            //if (ReservedPlayerData.localPlayerData.currentItemSlotIsReserved && !ConfigSettings.toggleFocusReservedHotbar.Value) return false;

            if (__instance.twoHanded || __instance.sinkingValue > 0.73f)
                return true;

            var interactRay = new Ray(__instance.gameplayCamera.transform.position, __instance.gameplayCamera.transform.forward);
            if (Physics.Raycast(interactRay, out var hit, __instance.grabDistance, INTERACTABLE_OBJECT_MASK))
            {
                if (hit.collider.gameObject.layer != 8 && hit.collider.tag == "PhysicsProp")
                {
                    var currentlyGrabbingObject = hit.collider.transform.gameObject.GetComponent<GrabbableObject>();
                    if (currentlyGrabbingObject != null && !__instance.inSpecialInteractAnimation && !currentlyGrabbingObject.isHeld && !currentlyGrabbingObject.isPocketed)
                    {
                        NetworkObject networkObject = currentlyGrabbingObject.NetworkObject;
                        if (networkObject != null && networkObject.IsSpawned)
                        {
                            if (SessionManager.TryGetUnlockedItemData(currentlyGrabbingObject, out var grabbingItemData))
                            {
                                localPlayerData.grabbingReservedItemData = grabbingItemData;
                                localPlayerData.grabbingReservedItem = currentlyGrabbingObject;
                                localPlayerData.previousHotbarIndex = Mathf.Clamp(__instance.currentItemSlot, 0, __instance.ItemSlots.Length - 1);
                                Plugin.Log("Beginning grab on reserved item: " + grabbingItemData.itemName + " Previous item slot: " + localPlayerData.previousHotbarIndex);
                            }
                        }
                    }
                }
            }

            return true;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "BeginGrabObject")]
        [HarmonyPostfix]
        private static void BeginGrabReservedItemPostfix(PlayerControllerB __instance)
        {
            // Fix some animations (or prevent swapping item animations)
            if (localPlayerData != null && localPlayerData.isGrabbingReservedItem && !localPlayerData.IsReservedItemSlot(localPlayerData.previousHotbarIndex))
            {
                SetSpecialGrabAnimationBool(__instance, false);
                SetSpecialGrabAnimationBool(__instance, localPlayerData.previouslyHeldItem != null, localPlayerData.previouslyHeldItem);
                __instance.playerBodyAnimator.SetBool("GrabValidated", value: true);
                __instance.playerBodyAnimator.SetBool("GrabInvalidated", value: false);
                __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimation");
                __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimationTwoHanded");
                if (localPlayerData.previouslyHeldItem != null)
                    __instance.playerBodyAnimator.ResetTrigger(localPlayerData.previouslyHeldItem.itemProperties.pocketAnim);
                __instance.twoHanded = localPlayerData.previouslyHeldItem != null ? localPlayerData.previouslyHeldItem.itemProperties.twoHanded : false;
                __instance.twoHandedAnimation = localPlayerData.previouslyHeldItem != null ? localPlayerData.previouslyHeldItem.itemProperties.twoHandedAnimation : false;
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "GrabObjectClientRpc")]
        [HarmonyPrefix]
        private static void GrabReservedItemClientRpcPrefix(bool grabValidated, NetworkObjectReference grabbedObject, PlayerControllerB __instance)
        {
            if ((!SyncManager.isSynced && !SyncManager.canUseModDisabledOnHost) || !NetworkHelper.IsClientExecStage(__instance))
                return;

            if (!ReservedPlayerData.allPlayerData.TryGetValue(__instance, out var playerData))
                return;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (grabValidated && grabbedObject.TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out GrabbableObject grabbingObject))
                {
                    if (SessionManager.TryGetUnlockedItemData(grabbingObject, out var grabbingItemData))
                    {
                        var grabbingReservedItemSlotData = playerData.GetFirstEmptySlotForReservedItem(grabbingItemData.itemName);
                        if (grabbingReservedItemSlotData != null)
                        {
                            playerData.grabbingReservedItemSlotData = grabbingReservedItemSlotData;
                            playerData.grabbingReservedItemData = grabbingItemData;
                            playerData.grabbingReservedItem = grabbingObject;
                            playerData.previousHotbarIndex = Mathf.Clamp(__instance.currentItemSlot, 0, __instance.ItemSlots.Length - 1);
                            return;
                        }
                    }
                }
            }
            playerData.grabbingReservedItemSlotData = null;
            playerData.grabbingReservedItemData = null;
            playerData.grabbingReservedItem = null;
            playerData.previousHotbarIndex = -1;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "GrabObjectClientRpc")]
        [HarmonyPostfix]
        private static void GrabReservedItemClientRpcPostfix(bool grabValidated, NetworkObjectReference grabbedObject, PlayerControllerB __instance)
        {
            if ((!SyncManager.isSynced && !SyncManager.canUseModDisabledOnHost) || !NetworkHelper.IsClientExecStage(__instance))
                return;

            if (!ReservedPlayerData.allPlayerData.TryGetValue(__instance, out var playerData) || !playerData.isGrabbingReservedItem)
                return;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (grabValidated && grabbedObject.TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out GrabbableObject grabbingObject))
                {
                    if (SessionManager.TryGetUnlockedItemData(grabbingObject, out var grabbingItemData))
                    {
                        if (!playerData.IsReservedItemSlot(playerData.previousHotbarIndex))
                        {
                            // Swap back to previous item slot
                            if (playerData.previouslyHeldItem != null)
                                playerData.previouslyHeldItem.EnableItemMeshes(true);
                            ReservedItemsPatcher.ForceEnableItemMesh(playerData.grabbingReservedItem, false); // Force disable

                            Traverse.Create(grabbingObject).Field("previousPlayerHeldBy").SetValue(__instance);

                            if (playerData.isLocalPlayer)
                            {
                                int hotbarIndex = playerData.reservedHotbarStartIndex + playerData.grabbingReservedItemSlotData.GetReservedItemSlotIndex();
                                HUDManager.Instance.itemSlotIconFrames[hotbarIndex].GetComponent<Animator>().SetBool("selectedSlot", false);
                                HUDManager.Instance.itemSlotIconFrames[playerData.previousHotbarIndex].GetComponent<Animator>().SetBool("selectedSlot", true);
                                HUDManager.Instance.itemSlotIconFrames[hotbarIndex].GetComponent<Animator>().Play("PanelLines", 0, 1);
                                HUDManager.Instance.itemSlotIconFrames[playerData.previousHotbarIndex].GetComponent<Animator>().Play("PanelEnlarge", 0, 1);
                            }
                            else
                            {
                                SwitchToItemSlot(__instance, playerData.previousHotbarIndex, null);
                                if (grabbingItemData.showOnPlayerWhileHolstered)
                                    grabbingObject.EnableItemMeshes(true);
                            }

                            SetSpecialGrabAnimationBool(__instance, false);
                            SetSpecialGrabAnimationBool(__instance, playerData.previouslyHeldItem != null, playerData.previouslyHeldItem);
                            __instance.playerBodyAnimator.SetBool("GrabValidated", value: true);
                            __instance.playerBodyAnimator.SetBool("GrabInvalidated", value: false);
                            __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimation");
                            __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimationTwoHanded");
                            if (playerData.previouslyHeldItem != null)
                                __instance.playerBodyAnimator.ResetTrigger(playerData.previouslyHeldItem.itemProperties.pocketAnim);
                            __instance.twoHanded = playerData.previouslyHeldItem != null ? playerData.previouslyHeldItem.itemProperties.twoHanded : false;
                            __instance.twoHandedAnimation = playerData.previouslyHeldItem != null ? playerData.previouslyHeldItem.itemProperties.twoHandedAnimation : false;
                        }

                        if (playerData.isLocalPlayer)
                            HUDPatcher.UpdateUI();
                        else
                        {
                            playerData.grabbingReservedItemSlotData = null;
                            playerData.grabbingReservedItemData = null;
                            playerData.grabbingReservedItem = null;
                            playerData.previousHotbarIndex = -1;
                        }

                        return;
                    }
                }
                else
                {
                    if (playerData.isLocalPlayer)
                    {
                        Plugin.LogWarning("Failed to validate ReservedItemGrab by the local player. Object id: " + grabbedObject.NetworkObjectId + ". Internal error?");
                        Traverse.Create(localPlayerController).Field("grabInvalidated").SetValue(true);
                    }
                    else
                        Plugin.LogWarning("Failed to validate ReservedItemGrab by player with id: " + __instance.name + ". Object id: " + grabbedObject.NetworkObjectId + ". Internal error?");
                }
            }

            playerData.grabbingReservedItemSlotData = null;
            playerData.grabbingReservedItemData = null;
            playerData.grabbingReservedItem = null;
            playerData.previousHotbarIndex = -1;
        }


        [HarmonyPatch(typeof(GrabbableObject), "GrabItemOnClient")]
        [HarmonyPrefix]
        private static void OnReservedItemGrabbed(GrabbableObject __instance)
        {
            IEnumerator OnReservedItemGrabbedEndOfFrame()
            {
                yield return new WaitForEndOfFrame();

                //if (!ReservedHotbarManager.isToggledInReservedSlots && !Keybinds.holdingModifierKey)
                if (localPlayerData.isGrabbingReservedItem)
                {
                    if (localPlayerData.previousHotbarIndex < 0 || localPlayerData.previousHotbarIndex >= localPlayerController.ItemSlots.Length || localPlayerData.IsReservedItemSlot(localPlayerData.previousHotbarIndex))
                        localPlayerData.previousHotbarIndex = 0;

                    SwitchToItemSlot(localPlayerController, localPlayerData.previousHotbarIndex, null);
                    __instance?.PocketItem();

                    SetSpecialGrabAnimationBool(localPlayerController, false);
                    SetSpecialGrabAnimationBool(localPlayerController, localPlayerData.previouslyHeldItem != null, localPlayerData.previouslyHeldItem);
                    localPlayerController.playerBodyAnimator.SetBool("GrabValidated", value: true);
                    localPlayerController.playerBodyAnimator.SetBool("GrabInvalidated", value: false);
                    localPlayerController.playerBodyAnimator.ResetTrigger("SwitchHoldAnimation");
                    localPlayerController.isGrabbingObjectAnimation = false;
                    localPlayerController.playerBodyAnimator.ResetTrigger("SwitchHoldAnimationTwoHanded");
                    if (localPlayerData.previouslyHeldItem != null)
                        localPlayerController.playerBodyAnimator.ResetTrigger(localPlayerData.previouslyHeldItem.itemProperties.pocketAnim);
                    localPlayerController.twoHanded = localPlayerData.previouslyHeldItem != null ? localPlayerData.previouslyHeldItem.itemProperties.twoHanded : false;
                    localPlayerController.twoHandedAnimation = localPlayerData.previouslyHeldItem != null ? localPlayerData.previouslyHeldItem.itemProperties.twoHandedAnimation : false;
                }

                localPlayerData.grabbingReservedItemSlotData = null;
                localPlayerData.grabbingReservedItemData = null;
                localPlayerData.grabbingReservedItem = null;
                localPlayerData.previousHotbarIndex = -1;
            }

            if (localPlayerData.grabbingReservedItemData != null && __instance == GetCurrentlyGrabbingObject(localPlayerController))
                localPlayerController.StartCoroutine(OnReservedItemGrabbedEndOfFrame());
        }


        [HarmonyPatch(typeof(PlayerControllerB), "SwitchToItemSlot")]
        [HarmonyPrefix]
        private static void UpdateLastSelectedHotbarIndex(int slot, PlayerControllerB __instance)
        {
            int currentSlot = __instance.currentItemSlot;
            if (ReservedPlayerData.allPlayerData.TryGetValue(__instance, out var playerData))
            {
                if (playerData.IsReservedItemSlot(currentSlot))
                    ReservedHotbarManager.indexInReservedHotbar = currentSlot;
                else
                    ReservedHotbarManager.indexInHotbar = currentSlot;
            }
        }

        
        [HarmonyPatch(typeof(PlayerControllerB), "SwitchToItemSlot")]
        [HarmonyPostfix]
        private static void UpdateFocusReservedHotbar(int slot, PlayerControllerB __instance)
        {
            if (!HUDPatcher.hasReservedItemSlotsAndEnabled || !ReservedPlayerData.allPlayerData.TryGetValue(__instance, out var playerData))
                return;
            
            bool wasInReservedHotbarSlots = playerData.inReservedHotbarSlots;
            playerData.inReservedHotbarSlots = playerData.IsReservedItemSlot(__instance.currentItemSlot);

            bool updateToggledSlots = false;
            if (wasInReservedHotbarSlots != playerData.inReservedHotbarSlots || (playerData.inReservedHotbarSlots && ReservedHotbarManager.isToggledInReservedSlots && ReservedHotbarManager.currentlyToggledItemSlots != null && !ReservedHotbarManager.currentlyToggledItemSlots.Contains(playerData.GetCurrentlySelectedReservedItemSlot())))
                updateToggledSlots = true;

            if (playerData.inReservedHotbarSlots)
            {
                ReservedHotbarManager.OnSwapToReservedHotbar();
                /*var currentReservedItemSlot = playerData.GetCurrentlySelectedReservedItemSlot();
                if (playerData.isLocalPlayer && !ConfigSettings.toggleFocusReservedHotbar.Value && !Keybinds.holdingModifierKey && !(ReservedHotbarManager.currentlyToggledItemSlots != null && ReservedHotbarManager.currentlyToggledItemSlots.Contains(currentReservedItemSlot)))
                {
                    ReservedHotbarManager.currentlyToggledItemSlots = new List<ReservedItemSlotData>() { currentReservedItemSlot };
                    HUDPatcher.UpdateToggledReservedItemSlotsUI();
                }*/
            }
            else
                ReservedHotbarManager.OnSwapToVanillaHotbar();

            if (updateToggledSlots)
                HUDPatcher.UpdateToggledReservedItemSlotsUI();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "FirstEmptyItemSlot")]
        [HarmonyPostfix]
        private static void GetReservedItemSlotPlacementIndex(ref int __result, PlayerControllerB __instance)
        {
            if (reservedHotbarSize <= 0 || !HUDPatcher.hasReservedItemSlotsAndEnabled)
                return;

            if (!ReservedPlayerData.allPlayerData.TryGetValue(__instance, out var playerData))
                return;

            var grabbingItemData = playerData.grabbingReservedItemData;
            // Try placing reserved item in compatible reserved item slot
            if (grabbingItemData != null)
            {
                var reservedItemSlot = playerData.GetFirstEmptySlotForReservedItem(grabbingItemData.itemName);
                if (reservedItemSlot != null)
                {
                    __result = reservedItemSlot.GetIndexInInventory(__instance);
                    return;
                }
                playerData.grabbingReservedItemSlotData = null;
                playerData.grabbingReservedItemData = null;
                playerData.grabbingReservedItem = null;
                playerData.previousHotbarIndex = -1;
            }

            // Prevent item from being placed in any empty reserved item slot
            if (playerData.IsReservedItemSlot(__result))
            {
                __result = -1;
                for (int i = 0; i < __instance.ItemSlots.Length; i++)
                {
                    if (!playerData.IsReservedItemSlot(i) && __instance.ItemSlots[i] == null)
                    {
                        __result = i;
                        break;
                    }
                }
            }
        }


        [HarmonyPatch(typeof(PlayerControllerB), "NextItemSlot")]
        [HarmonyPostfix]
        private static void OnNextItemSlot(ref int __result, bool forward, PlayerControllerB __instance)
        {
            if (reservedHotbarSize <= 0 || !HUDPatcher.hasReservedItemSlotsAndEnabled)
                return;

            if (!ReservedPlayerData.allPlayerData.TryGetValue(__instance, out var playerData))
                return;

            // Prevent scrolling other hotbar
            // Skip empty item slots (in reserved hotbar)
            bool currentlyInReservedSlots = playerData.inReservedHotbarSlots;
            bool resultInReservedSlots = playerData.IsReservedItemSlot(__result);

            //bool inReservedHotbar = playerData.IsReservedItemSlot(__result);
            bool switchToReservedItemSlot = currentlyInReservedSlots;

            if (currentlyInReservedSlots)
            {
                var resultReservedItemSlot = SessionManager.GetUnlockedReservedItemSlot(__result - playerData.reservedHotbarStartIndex);
                if (ReservedHotbarManager.isToggledInReservedSlots && !ConfigSettings.toggleFocusReservedHotbar.Value && !Keybinds.holdingModifierKey && ReservedHotbarManager.currentlyToggledItemSlots != null && (!resultInReservedSlots || playerData.itemSlots[__result] == null || !ReservedHotbarManager.currentlyToggledItemSlots.Contains(resultReservedItemSlot)))
                {
                    __result = ReservedHotbarManager.indexInHotbar;
                    return;
                }
            }

            if (resultInReservedSlots == switchToReservedItemSlot && (!resultInReservedSlots || __instance.ItemSlots[__result] != null))
                return;

            int direction = forward ? 1 : -1;
            __result = __instance.currentItemSlot + direction;
            __result = __result < 0 ? __instance.ItemSlots.Length - 1 : (__result >= __instance.ItemSlots.Length ? 0 : __result);
            resultInReservedSlots = playerData.IsReservedItemSlot(__result);

            if (!switchToReservedItemSlot)
            {
                if (resultInReservedSlots)
                    __result = forward ? (playerData.reservedHotbarStartIndex + reservedHotbarSize) % __instance.ItemSlots.Length : playerData.reservedHotbarStartIndex - 1;
            }
            else
            {
                __result = !resultInReservedSlots ? (forward ? playerData.reservedHotbarStartIndex : playerData.reservedHotbarStartIndex + reservedHotbarSize - 1) : __result;
                int numHeldReservedItems = playerData.GetNumHeldReservedItems();
                while (numHeldReservedItems > 0 && __result != playerData.currentItemSlot && __instance.ItemSlots[__result] == null)
                {
                    __result = __result + direction;
                    __result = !playerData.IsReservedItemSlot(__result) ? (forward ? playerData.reservedHotbarStartIndex : playerData.reservedHotbarStartIndex + reservedHotbarSize - 1) : __result;
                }
            }
        }


        [HarmonyPatch(typeof(HUDManager), "ClearControlTips")]
        [HarmonyPrefix]
        private static bool PreventClearControlTipsGrabbingReservedItem(HUDManager __instance) => ReservedPlayerData.localPlayerData == null || ReservedPlayerData.localPlayerData.grabbingReservedItem == null;


        [HarmonyPatch(typeof(GrabbableObject), "SetControlTipsForItem")]
        [HarmonyPrefix]
        private static bool PreventUpdateControlTipsGrabbingReservedItem(GrabbableObject __instance) => ReservedPlayerData.localPlayerData == null || ReservedPlayerData.localPlayerData.grabbingReservedItem != __instance;


        static GrabbableObject GetCurrentlyGrabbingObject(PlayerControllerB playerController) => (GrabbableObject)Traverse.Create(playerController).Field("currentlyGrabbingObject").GetValue();
        static void SetCurrentlyGrabbingObject(PlayerControllerB playerController, GrabbableObject grabbable) => Traverse.Create(playerController).Field("currentlyGrabbingObject").SetValue(grabbable);


        public static bool ReservedItemIsBeingGrabbed(GrabbableObject grabbableObject)
        {
            if (grabbableObject == null)
                return false;
            foreach (var playerData in ReservedPlayerData.allPlayerData.Values)
            {
                if (grabbableObject == playerData.grabbingReservedItem)
                    return true;
            }
            return false;
        }


        public static void SetSpecialGrabAnimationBool(PlayerControllerB playerController, bool setTrue, GrabbableObject currentItem = null)
        {
            MethodInfo method = playerController.GetType().GetMethod("SetSpecialGrabAnimationBool", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(playerController, new object[] { setTrue, currentItem });
        }


        public static void SwitchToItemSlot(PlayerControllerB playerController, int slot, GrabbableObject fillSlotWithItem = null)
        {
            MethodInfo method = playerController.GetType().GetMethod("SwitchToItemSlot", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(playerController, new object[] { slot, fillSlotWithItem });
            if (ReservedPlayerData.allPlayerData.TryGetValue(playerController, out var playerData))
                playerData.timeSinceSwitchingSlots = 0;
        }
    }
}