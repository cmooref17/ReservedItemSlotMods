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

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    public static class HUDPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static bool localPlayerUsingController { get { return StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerUsingController : false; } }
        static bool usingController = false;

        static float itemSlotWidth;
        static float itemSlotSpacing;
        static float xPos;
        static TextMeshProUGUI hotkeyTooltip;

        public static List<Image> reservedItemSlots = new List<Image>();
        public static bool hasReservedItemSlotsAndEnabled { get { return reservedItemSlots != null && reservedItemSlots.Count > 0 && reservedItemSlots[0].gameObject.activeSelf && reservedItemSlots[0].enabled; } }

        public static HashSet<ReservedItemSlotData> toggledReservedItemSlots = new HashSet<ReservedItemSlotData>();
        static bool lerpToggledItemSlotFrames = false;
        static float largestPositionDifference = 0;


        [HarmonyPatch(typeof(HUDManager), "Awake")]
        [HarmonyPostfix]
        public static void Initialize(HUDManager __instance)
        {
            var canvasScaler = __instance.itemSlotIconFrames[0].GetComponentInParent<CanvasScaler>();
            var aspectRatioFitter = __instance.itemSlotIconFrames[0].GetComponentInParent<AspectRatioFitter>();
            itemSlotWidth = __instance.itemSlotIconFrames[0].GetComponent<RectTransform>().sizeDelta.x;
            itemSlotSpacing = ((9f / 8f) * itemSlotWidth);
            xPos = (canvasScaler.referenceResolution.x / 2) / aspectRatioFitter.aspectRatio - itemSlotWidth / 4;
            reservedItemSlots.Clear();
        }


        [HarmonyPatch(typeof(StartOfRound), "Update")]
        [HarmonyPrefix]
        public static void UpdateUsingController(StartOfRound __instance)
        {
            if (__instance.localPlayerController == null || hotkeyTooltip == null || !hotkeyTooltip.gameObject.activeSelf || !hotkeyTooltip.enabled)
                return;

            if (__instance.localPlayerUsingController != usingController)
            {
                usingController = __instance.localPlayerUsingController;
                UpdateHotkeyTooltipText();
            }

            if (lerpToggledItemSlotFrames)
            {
                if (largestPositionDifference < 2 && largestPositionDifference != -1)
                    lerpToggledItemSlotFrames = false;

                for (int i = 0; i < SessionManager.numReservedItemSlotsUnlocked; i++)
                {
                    var reservedItemSlot = SessionManager.GetUnlockedReservedItemSlot(i);
                    var itemSlotFrame = HUDManager.Instance.itemSlotIconFrames[ReservedPlayerData.localPlayerData.reservedHotbarStartIndex + i];
                    bool anchorRight = reservedItemSlot.slotPriority >= 0 || !ConfigSettings.displayNegativePrioritySlotsLeftSideOfScreen.Value;
                    Vector2 itemSlotFramePosition = itemSlotFrame.rectTransform.anchoredPosition;
                    itemSlotFramePosition.x = anchorRight ? xPos : -xPos;

                    if (ReservedHotbarManager.isToggledInReservedSlots && ReservedHotbarManager.currentlyToggledItemSlots != null && ReservedHotbarManager.currentlyToggledItemSlots.Contains(reservedItemSlot))
                        itemSlotFramePosition.x += (itemSlotWidth / 2) * (anchorRight ? -1 : 1);

                    float positionDifference = Mathf.Abs(itemSlotFramePosition.x - itemSlotFrame.rectTransform.anchoredPosition.x);
                    largestPositionDifference = Mathf.Max(largestPositionDifference, positionDifference);
                    if (lerpToggledItemSlotFrames)
                        itemSlotFrame.rectTransform.anchoredPosition = Vector2.Lerp(itemSlotFrame.rectTransform.anchoredPosition, itemSlotFramePosition, Time.deltaTime * 10);
                    else
                        itemSlotFrame.rectTransform.anchoredPosition = itemSlotFramePosition;
                }
            }
        }


        public static void OnUpdateReservedItemSlots()
        {
            if (reservedItemSlots == null || SessionManager.numReservedItemSlotsUnlocked <= 0 || reservedItemSlots.Count == SessionManager.numReservedItemSlotsUnlocked)
                return;

            var newItemSlotFrames = new List<Image>(HUDManager.Instance.itemSlotIconFrames);
            var newItemSlotIcons = new List<Image>(HUDManager.Instance.itemSlotIcons);

            for (int i = reservedItemSlots.Count; i < SessionManager.numReservedItemSlotsUnlocked; i++)
            {
                Image itemSlotFrame = GameObject.Instantiate(newItemSlotFrames[0], newItemSlotFrames[0].transform.parent);
                Image itemSlotIcon = itemSlotFrame.transform.GetChild(0).GetComponent<Image>();
                itemSlotFrame.transform.localScale = newItemSlotFrames[0].transform.localScale;
                itemSlotFrame.rectTransform.eulerAngles = newItemSlotFrames[0].rectTransform.eulerAngles;
                itemSlotIcon.rectTransform.eulerAngles = newItemSlotIcons[0].rectTransform.eulerAngles;

                CanvasGroup canvasGroup = itemSlotFrame.gameObject.AddComponent<CanvasGroup>();
                canvasGroup.ignoreParentGroups = ConfigSettings.preventReservedItemSlotFade.Value;
                canvasGroup.alpha = 1;

                int insertIndex = ReservedPlayerData.localPlayerData.reservedHotbarStartIndex + reservedItemSlots.Count;
                newItemSlotFrames.Insert(insertIndex, itemSlotFrame);
                newItemSlotIcons.Insert(insertIndex, itemSlotIcon);
                reservedItemSlots.Add(itemSlotFrame);
            }

            HUDManager.Instance.itemSlotIconFrames = newItemSlotFrames.ToArray();
            HUDManager.Instance.itemSlotIcons = newItemSlotIcons.ToArray();

            UpdateUI();
        }


        public static void UpdateUI()
        {
            if (reservedItemSlots.Count != SessionManager.numReservedItemSlotsUnlocked)
            {
                Plugin.LogError("Called UpdateUI with mismatched unlocked reserved item slots and reserved item slot hud elements.");
                return;
            }

            int positiveIndex = 0;
            int negativeIndex = 0;

            for (int i = 0; i < SessionManager.numReservedItemSlotsUnlocked; i++)
            {
                var reservedItemSlot = SessionManager.GetUnlockedReservedItemSlot(i);
                var itemSlotFrame = HUDManager.Instance.itemSlotIconFrames[ReservedPlayerData.localPlayerData.reservedHotbarStartIndex + i];
                var itemSlotIcon = HUDManager.Instance.itemSlotIcons[ReservedPlayerData.localPlayerData.reservedHotbarStartIndex + i];

                itemSlotFrame.name = "Slot" + i + " [ReservedItemSlot] (" + reservedItemSlot.slotName + ")";

                Vector2 hudPosition = default;
                if (reservedItemSlot.slotPriority >= 0 || !ConfigSettings.displayNegativePrioritySlotsLeftSideOfScreen.Value)
                {
                    hudPosition.x = xPos;
                    hudPosition.y = HUDManager.Instance.itemSlotIconFrames[0].rectTransform.anchoredPosition.y + itemSlotSpacing * positiveIndex;
                    positiveIndex++;
                }
                else
                {
                    hudPosition.x = -xPos;
                    hudPosition.y = HUDManager.Instance.itemSlotIconFrames[0].rectTransform.anchoredPosition.y + itemSlotSpacing * negativeIndex;
                    negativeIndex++;
                }

                itemSlotFrame.rectTransform.anchoredPosition = hudPosition;

                var reservedGrabbableObject = ReservedPlayerData.localPlayerData.GetReservedItem(reservedItemSlot);
                if (reservedGrabbableObject != null)
                {
                    itemSlotIcon.enabled = true;
                    itemSlotIcon.sprite = reservedGrabbableObject.itemProperties.itemIcon;
                }
                else
                {
                    itemSlotIcon.enabled = false;
                    itemSlotIcon.sprite = null;
                }
            }


            if (SessionManager.numReservedItemSlotsUnlocked > 0)
            {
                if (hotkeyTooltip == null)
                    hotkeyTooltip = new GameObject("ReservedItemSlotTooltip", new Type[] { typeof(RectTransform), typeof(TextMeshProUGUI) }).GetComponent<TextMeshProUGUI>();

                RectTransform tooltipTransform = hotkeyTooltip.rectTransform;
                tooltipTransform.transform.parent = reservedItemSlots[0].transform;
                tooltipTransform.localScale = Vector3.one;
                tooltipTransform.sizeDelta = new Vector2(HUDManager.Instance.itemSlotIconFrames[0].rectTransform.sizeDelta.x * 2, 10);
                tooltipTransform.pivot = Vector2.one / 2;
                tooltipTransform.anchoredPosition3D = new Vector3(0, -(tooltipTransform.sizeDelta.x / 2) * 1.2f, 0);
                hotkeyTooltip.font = HUDManager.Instance.controlTipLines[0].font;
                hotkeyTooltip.fontSize = 7;
                hotkeyTooltip.alignment = TextAlignmentOptions.Center;
                UpdateHotkeyTooltipText();
            }
        }


        public static void UpdateHotkeyTooltipText()
        {
            if (localPlayerController == null || hotkeyTooltip == null || Keybinds.FocusReservedHotbarAction == null || Plugin.IsModLoaded("com.potatoepet.AdvancedCompany"))
                return;
            int bindingIndex = localPlayerUsingController ? 1 : 0;
            string displayName = KeybindDisplayNames.GetKeybindDisplayName(Keybinds.FocusReservedHotbarAction.bindings[bindingIndex].effectivePath);
            hotkeyTooltip.text = ConfigSettings.toggleFocusReservedHotbar.Value ? string.Format("Toggle: [{0}]", displayName) : string.Format("Hold: [{0}]", displayName);
        }


        public static void UpdateToggledReservedItemSlotsUI()
        {
            if (ReservedHotbarManager.currentlyToggledItemSlots != null)
                toggledReservedItemSlots = new HashSet<ReservedItemSlotData>(ReservedHotbarManager.currentlyToggledItemSlots);
            else
                toggledReservedItemSlots.Clear();

            lerpToggledItemSlotFrames = true;
            largestPositionDifference = -1;
        }
    }
}