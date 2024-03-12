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

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    public static class PlayerPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        static Dictionary<PlayerControllerB, ReservedPlayerData> allPlayerData { get { return ReservedPlayerData.allPlayerData; } }
        static ReservedPlayerData localPlayerData { get { return ReservedPlayerData.localPlayerData; }  }

        public static int INTERACTABLE_OBJECT_MASK { get; private set; }
        public static int vanillaHotbarSize = 4;
        public static int reservedHotbarSize { get { return SessionManager.numReservedItemSlotsUnlocked; } }

        static bool initialized = false;


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void InitSession(StartOfRound __instance)
        {
            initialized = false;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "Awake")]
        [HarmonyPostfix]
        public static void InitializePlayerController(PlayerControllerB __instance)
        {
            if (!initialized)
            {
                vanillaHotbarSize = __instance.ItemSlots.Length;
                INTERACTABLE_OBJECT_MASK = (int)Traverse.Create(__instance).Field("interactableObjectsMask").GetValue();
                initialized = true;
            }
        }

        
        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPostfix]
        public static void InitializePlayerControllerLate(PlayerControllerB __instance)
        {
            var playerData = new ReservedPlayerData(__instance);
            if (!allPlayerData.ContainsKey(__instance))
                allPlayerData.Add(__instance, playerData);
        }
        

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        public static void CheckForChangedInventorySize(PlayerControllerB __instance)
        {
            if (SessionManager.preGame)
                return;

            var playerData = allPlayerData[__instance];
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
                Plugin.LogError("Set new reserved start index to slot: " + playerData.reservedHotbarStartIndex + " . Maybe share these logs with Flip? :)");
            if (playerData.reservedHotbarEndIndexExcluded - 1 >= playerData.playerController.ItemSlots.Length)
                Plugin.LogError("Set new reserved start index to slot: " + playerData.reservedHotbarStartIndex + " Last reserved slot index: " + (playerData.reservedHotbarEndIndexExcluded - 1) + " Inventory size: " + playerData.playerController.ItemSlots.Length + ". Maybe share these logs with Flip? :)");

            if (__instance == localPlayerController)
                HUDPatcher.UpdateUI();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "BeginGrabObject")]
        [HarmonyPrefix]
        public static bool GrabReservedItemPrefix(PlayerControllerB __instance)
        {
            if ((!SyncManager.isSynced && !SyncManager.canUseModDisabledOnHost) || !HUDPatcher.hasReservedItemSlotsAndEnabled)
                return true;

            ReservedPlayerData.localPlayerData.grabbingReservedItemData = null;
            ReservedPlayerData.localPlayerData.grabbingReservedItem = null;
            ReservedPlayerData.localPlayerData.previousHotbarIndex = -1;

            if (ReservedPlayerData.localPlayerData.currentItemSlotIsReserved && !ConfigSettings.toggleFocusReservedHotbar.Value)
                return false;

            if (__instance.twoHanded || __instance.sinkingValue > 0.73f)
                return true;

            var interactRay = new Ray(__instance.gameplayCamera.transform.position, __instance.gameplayCamera.transform.forward);
            if (Physics.Raycast(interactRay, out var hit, __instance.grabDistance, PlayerPatcher.INTERACTABLE_OBJECT_MASK) && hit.collider.gameObject.layer != 8 && hit.collider.tag == "PhysicsProp")
            {
                var currentlyGrabbingObject = hit.collider.transform.gameObject.GetComponent<GrabbableObject>();
                if (currentlyGrabbingObject != null && !__instance.inSpecialInteractAnimation && !currentlyGrabbingObject.isHeld && !currentlyGrabbingObject.isPocketed)
                {
                    NetworkObject networkObject = currentlyGrabbingObject.NetworkObject;
                    if (networkObject != null && networkObject.IsSpawned)
                    {
                        if (SessionManager.TryGetReservedItemData(currentlyGrabbingObject, out var grabbingItemData))
                        {
                            Plugin.Log("Beginning grab on reserved item: " + grabbingItemData.itemName);
                            ReservedPlayerData.localPlayerData.grabbingReservedItemData = grabbingItemData;
                            ReservedPlayerData.localPlayerData.grabbingReservedItem = currentlyGrabbingObject;
                            ReservedPlayerData.localPlayerData.previousHotbarIndex = __instance.currentItemSlot;
                        }
                    }
                }
            }

            return true;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "BeginGrabObject")]
        [HarmonyPostfix]
        public static void GrabReservedItemPostfix(PlayerControllerB __instance)
        {
            //if (grabbingReservedItemInfoLocal != null)
            if (ReservedPlayerData.localPlayerData.grabbingReservedItemData != null)
                SetSpecialGrabAnimationBool(__instance, false, null);
        }


        [HarmonyPatch(typeof(PlayerControllerB), "GrabObjectClientRpc")]
        [HarmonyPrefix]
        public static void GrabReservedItemClientRpcPrefix(bool grabValidated, NetworkObjectReference grabbedObject, PlayerControllerB __instance)
        {
            if ((!SyncManager.isSynced && !SyncManager.canUseModDisabledOnHost) || !NetworkHelper.IsClientExecStage(__instance))
                return;

            var playerData = ReservedPlayerData.allPlayerData[__instance];
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (grabValidated && grabbedObject.TryGet(out NetworkObject networkObject))
                {
                    GrabbableObject grabbingObject = networkObject.GetComponent<GrabbableObject>();
                    if (SessionManager.TryGetUnlockedReservedItemData(grabbingObject, out var grabbingItemData))
                    {
                        var grabbingReservedItemSlotData = playerData.GetFirstEmptySlotForItem(grabbingItemData.itemName);
                        if (grabbingReservedItemSlotData != null)
                        {
                            ReservedPlayerData.localPlayerData.grabbingReservedItemSlotData = ReservedPlayerData.localPlayerData.GetFirstEmptySlotForItem(grabbingItemData.itemName);
                            if (ReservedPlayerData.localPlayerData.grabbingReservedItemSlotData != null)
                            {
                                Plugin.Log("OnGrabReservedItem for Player: " + __instance.name + " Item: " + grabbingItemData);
                                playerData.grabbingReservedItemSlotData = grabbingReservedItemSlotData;
                                playerData.grabbingReservedItemData = grabbingItemData;
                                playerData.grabbingReservedItem = grabbingObject;
                                playerData.previousHotbarIndex = __instance.currentItemSlot;
                                return;
                            }
                        }
                    }
                }
            }
            playerData.grabbingReservedItemData = null;
            playerData.grabbingReservedItem = null;
            playerData.previousHotbarIndex = -1;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "GrabObjectClientRpc")]
        [HarmonyPostfix]
        public static void GrabReservedItemClientRpcPostfix(bool grabValidated, NetworkObjectReference grabbedObject, PlayerControllerB __instance)
        {
            if ((!SyncManager.isSynced && !SyncManager.canUseModDisabledOnHost) || !NetworkHelper.IsClientExecStage(__instance))
                return;

            var playerData = ReservedPlayerData.allPlayerData[__instance];
            if (playerData.grabbingReservedItemData == null)
                return;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (grabValidated && grabbedObject.TryGet(out NetworkObject networkObject))
                {
                    GrabbableObject grabbingObject = networkObject.GetComponent<GrabbableObject>();
                    if (SessionManager.TryGetReservedItemData(grabbingObject, out var grabbingItemData))
                    {
                        SetSpecialGrabAnimationBool(__instance, playerData.previouslyHeldItem != null, playerData.previouslyHeldItem);
                        __instance.playerBodyAnimator.Play(__instance.playerBodyAnimator.GetCurrentAnimatorStateInfo(2).shortNameHash, 2, 1);

                        if (playerData.previouslyHeldItem != null)
                            playerData.previouslyHeldItem.EnableItemMeshes(true);

                        ForceDisableItemMesh(playerData.grabbingReservedItem);

                        Traverse.Create(grabbingObject).Field("previousPlayerHeldBy").SetValue(__instance);
                        if (__instance != localPlayerController)
                        {
                            SwitchToItemSlot(__instance, playerData.previousHotbarIndex, null);
                            __instance.playerBodyAnimator.Play(__instance.playerBodyAnimator.GetCurrentAnimatorStateInfo(2).shortNameHash, 2, 1);
                            playerData.grabbingReservedItemData = null;
                            playerData.grabbingReservedItem = null;
                            playerData.previousHotbarIndex = -1;
                        }
                        else
                        {
                            int hotbarIndex = playerData.reservedHotbarStartIndex + playerData.grabbingReservedItemSlotData.GetReservedItemSlotIndex();
                            HUDManager.Instance.itemSlotIconFrames[hotbarIndex].GetComponent<Animator>().SetBool("selectedSlot", false);
                            HUDManager.Instance.itemSlotIconFrames[playerData.previousHotbarIndex].GetComponent<Animator>().SetBool("selectedSlot", true);
                            HUDManager.Instance.itemSlotIconFrames[hotbarIndex].GetComponent<Animator>().Play("PanelLines", 0, 1);
                            HUDManager.Instance.itemSlotIconFrames[playerData.previousHotbarIndex].GetComponent<Animator>().Play("PanelEnlarge", 0, 1);
                        }

                        return;
                    }
                }
                else
                {
                    if (__instance == localPlayerController)
                    {
                        Plugin.Log("Failed to validate ReservedItemGrab by the local player. Object id: " + grabbedObject.NetworkObjectId + ".");
                        Traverse.Create(localPlayerController).Field("grabInvalidated").SetValue(true);
                    }
                    else
                        Plugin.Log("Failed to validate ReservedItemGrab by player with id: " + __instance.name + ". Object id: " + grabbedObject.NetworkObjectId + ".");
                }
            }

            playerData.grabbingReservedItemData = null;
            playerData.grabbingReservedItem = null;
            playerData.previousHotbarIndex = -1;
        }


        [HarmonyPatch(typeof(GrabbableObject), "GrabItemOnClient")]
        [HarmonyPrefix]
        public static void OnReservedItemGrabbed(GrabbableObject __instance)
        {
            IEnumerator OnReservedItemGrabbedEndOfFrame()
            {
                yield return new WaitForEndOfFrame();
                SwitchToItemSlot(localPlayerController, ReservedPlayerData.localPlayerData.previousHotbarIndex, null);
                ReservedPlayerData.localPlayerData.grabbingReservedItemData = null;
                ReservedPlayerData.localPlayerData.grabbingReservedItem = null;
                ReservedPlayerData.localPlayerData.previousHotbarIndex = -1;
                __instance.EnableItemMeshes(false);
                __instance.PocketItem();
                localPlayerController.playerBodyAnimator.Play(localPlayerController.playerBodyAnimator.GetCurrentAnimatorStateInfo(2).shortNameHash, 2, 1);
            }

            if (ReservedPlayerData.localPlayerData.grabbingReservedItemData == null || __instance != GetCurrentlyGrabbingObject(localPlayerController))
                return;

            localPlayerController.StartCoroutine(OnReservedItemGrabbedEndOfFrame());
        }


        [HarmonyPatch(typeof(PlayerControllerB), "SwitchToItemSlot")]
        [HarmonyPostfix]
        public static void UpdateFocusReservedHotbar(int slot, PlayerControllerB __instance)
        {
            if (!HUDPatcher.hasReservedItemSlotsAndEnabled || !ReservedPlayerData.allPlayerData.TryGetValue(__instance, out var playerData))
                return;
            bool wasInReservedHotbarSlots = playerData.inReservedHotbarSlots;
            playerData.inReservedHotbarSlots = playerData.IsReservedItemSlot(__instance.currentItemSlot);

            bool updateToggledSlots = false;
            if (wasInReservedHotbarSlots != playerData.inReservedHotbarSlots || (playerData.inReservedHotbarSlots && ReservedHotbarManager.isToggledInReservedSlots && ReservedHotbarManager.currentlyToggledItemSlots != null && !ReservedHotbarManager.currentlyToggledItemSlots.Contains(playerData.GetCurrentlySelectedReservedItemSlot())))
                updateToggledSlots = true;

            if (playerData.inReservedHotbarSlots)
                ReservedHotbarManager.OnSwapToReservedHotbar();
            else
                ReservedHotbarManager.OnSwapToVanillaHotbar();

            if (updateToggledSlots)
                HUDPatcher.UpdateToggledReservedItemSlotsUI();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "FirstEmptyItemSlot")]
        [HarmonyPostfix]
        public static void GetReservedItemSlotPlacementIndex(ref int __result, PlayerControllerB __instance)
        {
            if (reservedHotbarSize <= 0 || !HUDPatcher.hasReservedItemSlotsAndEnabled)
                return;

            var playerData = ReservedPlayerData.allPlayerData[__instance];

            var grabbingItemData = playerData.grabbingReservedItemData;
            if (grabbingItemData != null)
            {
                var reservedItemSlot = playerData.GetFirstEmptySlotForItem(grabbingItemData.itemName);
                if (reservedItemSlot != null)
                {
                    __result = playerData.reservedHotbarStartIndex + reservedItemSlot.GetReservedItemSlotIndex();
                    return;
                }
                playerData.grabbingReservedItemData = null;
                playerData.grabbingReservedItem = null;
                playerData.previousHotbarIndex = -1;
            }

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
        public static void OnNextItemSlot(ref int __result, bool forward, PlayerControllerB __instance)
        {
            if (reservedHotbarSize <= 0 || !HUDPatcher.hasReservedItemSlotsAndEnabled)
                return;

            // Prevent scrolling other hotbar
            // Skip empty item slots (in reserved hotbar)
            var playerData = ReservedPlayerData.allPlayerData[__instance];
            bool inReservedHotbar = playerData.IsReservedItemSlot(__result);
            bool switchToReservedItemSlot = playerData.inReservedHotbarSlots;

            ReservedItemSlotData reservedItemSlot = null;
            if (__result >= playerData.reservedHotbarStartIndex && __result < playerData.reservedHotbarStartIndex + reservedHotbarSize)
            {
                reservedItemSlot = SessionManager.unlockedReservedItemSlots[__result - playerData.reservedHotbarStartIndex];
                if (inReservedHotbar && playerData.inReservedHotbarSlots && ReservedHotbarManager.isToggledInReservedSlots && !ConfigSettings.toggleFocusReservedHotbar.Value && (!ReservedHotbarManager.currentlyToggledItemSlots.Contains(reservedItemSlot) || __instance.ItemSlots[__result] == null))
                    switchToReservedItemSlot = false;
            }
            //if (inReservedHotbar && playerData.inReservedHotbarSlots && ReservedHotbarManager.isToggledInReservedSlots && !ConfigSettings.toggleFocusReservedHotbar.Value && (!ReservedHotbarManager.currentlyToggledItemSlots.Contains(playerData.GetCurrentlySelectedReservedItemSlot()) || __instance.ItemSlots[__result] == null)) switchToReservedItemSlot = false;

            if (inReservedHotbar == switchToReservedItemSlot && (!inReservedHotbar || __instance.ItemSlots[__result] != null))
                return;

            int direction = forward ? 1 : -1;
            __result = __instance.currentItemSlot + direction;
            __result = __result < 0 ? __instance.ItemSlots.Length - 1 : (__result >= __instance.ItemSlots.Length ? 0 : __result);
            bool resultInReservedHotbarSlots = playerData.IsReservedItemSlot(__result);

            if (!switchToReservedItemSlot)
            {
                if (resultInReservedHotbarSlots)
                    __result = forward ? (playerData.reservedHotbarStartIndex + reservedHotbarSize) % __instance.ItemSlots.Length : playerData.reservedHotbarStartIndex - 1;
            }
            else
            {
                __result = !resultInReservedHotbarSlots ? (forward ? playerData.reservedHotbarStartIndex : playerData.reservedHotbarStartIndex + reservedHotbarSize - 1) : __result;
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
        public static bool PreventClearControlTipsGrabbingReservedItem(HUDManager __instance) => ReservedPlayerData.localPlayerData == null || ReservedPlayerData.localPlayerData.grabbingReservedItem == null;


        [HarmonyPatch(typeof(GrabbableObject), "SetControlTipsForItem")]
        [HarmonyPrefix]
        public static bool PreventUpdateControlTipsGrabbingReservedItem(GrabbableObject __instance) => ReservedPlayerData.localPlayerData == null || ReservedPlayerData.localPlayerData.grabbingReservedItem != __instance;


        static GrabbableObject GetCurrentlyGrabbingObject(PlayerControllerB playerController) => (GrabbableObject)Traverse.Create(playerController).Field("currentlyGrabbingObject").GetValue();
        static void SetCurrentlyGrabbingObject(PlayerControllerB playerController, GrabbableObject grabbable) => Traverse.Create(playerController).Field("currentlyGrabbingObject").SetValue(grabbable);


        public static void ForceDisableItemMesh(GrabbableObject grabbableObject)
        {
            foreach (var renderer in grabbableObject.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.name.Contains("ScanNode") && !renderer.gameObject.CompareTag("DoNotSet") && !renderer.gameObject.CompareTag("InteractTrigger"))
                    renderer.enabled = false;
            }
        }


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
            var playerData = ReservedPlayerData.allPlayerData[playerController];
            MethodInfo method = playerController.GetType().GetMethod("SwitchToItemSlot", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(playerController, new object[] { slot, fillSlotWithItem });
            playerData.timeSinceSwitchingSlots = 0;
        }
    }
}