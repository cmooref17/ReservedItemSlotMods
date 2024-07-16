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
using UnityEngine.Assertions;

namespace ReservedItemSlotCore.Patches
{
    [HarmonyPatch]
    public static class HUDPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static ReservedPlayerData localPlayerData { get { return ReservedPlayerData.localPlayerData; } }
        public static bool localPlayerUsingController { get { return StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerUsingController : false; } }
        private static bool usingController = false;

        private static float itemSlotWidth;
        internal static float itemSlotSpacing;
        private static float defaultItemSlotPosX; // Negative for left slots
        private static float defaultItemSlotPosY; // First slot
        private static float defaultItemSlotSpacing;
        private static Vector2 defaultItemSlotSize;
        private static Vector2 defaultItemIconSize;
        private static TextMeshProUGUI hotkeyTooltip;

        private static float currentItemSlotScale { get { return itemSlotWidth / defaultItemSlotSize.x; } }

        public static List<Image> reservedItemSlots = new List<Image>();
        public static bool hasReservedItemSlotsAndEnabled { get { return reservedItemSlots != null && reservedItemSlots.Count > 0 && reservedItemSlots[0].gameObject.activeSelf && reservedItemSlots[0].enabled; } }

        public static HashSet<ReservedItemSlotData> toggledReservedItemSlots = new HashSet<ReservedItemSlotData>();
        private static bool lerpToggledItemSlotFrames = false;
        private static float largestPositionDifference = 0;

        private static bool currentApplyHotbarPlusSize;
        private static bool currentHideEmptySlots;


        [HarmonyPatch(typeof(HUDManager), "Awake")]
        [HarmonyPostfix]
        public static void Initialize(HUDManager __instance)
        {
            var canvasScaler = __instance.itemSlotIconFrames[0].GetComponentInParent<CanvasScaler>();
            var aspectRatioFitter = __instance.itemSlotIconFrames[0].GetComponentInParent<AspectRatioFitter>();
            itemSlotWidth = __instance.itemSlotIconFrames[0].rectTransform.sizeDelta.x;
            itemSlotSpacing = ((9f / 8f) * itemSlotWidth);
            defaultItemSlotPosX = (canvasScaler.referenceResolution.x / 2) / aspectRatioFitter.aspectRatio - itemSlotWidth / 4;

            defaultItemSlotSpacing = itemSlotSpacing;
            defaultItemSlotSize = __instance.itemSlotIconFrames[0].rectTransform.sizeDelta;
            defaultItemIconSize = __instance.itemSlotIcons[0].rectTransform.sizeDelta;
            defaultItemSlotPosY = __instance.itemSlotIconFrames[0].rectTransform.anchoredPosition.y;

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

            LerpItemSlotFrames();
        }


        private static void LerpItemSlotFrames()
        {
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
                    itemSlotFramePosition.x = (defaultItemSlotPosX + (defaultItemSlotSize.x - itemSlotWidth) / 2f) * (anchorRight ? 1 : -1);

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
                var itemSlotFrameObj = GameObject.Instantiate(newItemSlotFrames[0].gameObject, newItemSlotFrames[0].transform.parent);
                Image itemSlotFrame = itemSlotFrameObj.GetComponent<Image>();
                Image itemSlotIcon = itemSlotFrame.transform.GetChild(0).GetComponent<Image>();
                itemSlotFrame.transform.localScale = newItemSlotFrames[0].transform.localScale;
                itemSlotFrame.rectTransform.eulerAngles = newItemSlotFrames[0].rectTransform.eulerAngles;
                itemSlotIcon.rectTransform.eulerAngles = newItemSlotIcons[0].rectTransform.eulerAngles;

                CanvasGroup canvasGroup = itemSlotFrame.gameObject.AddComponent<CanvasGroup>();
                canvasGroup.ignoreParentGroups = ConfigSettings.preventReservedItemSlotFade.Value;
                canvasGroup.alpha = 1;

                itemSlotFrame.fillMethod = newItemSlotFrames[0].fillMethod;
                itemSlotFrame.sprite = newItemSlotFrames[0].sprite;
                itemSlotFrame.material = newItemSlotFrames[0].material;
                if (Plugin.IsModLoaded("xuxiaolan.hotbarrd"))
                    itemSlotFrame.overrideSprite = newItemSlotFrames[0].overrideSprite;

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
            RectTransform tooltipParent = null;

            for (int i = 0; i < SessionManager.numReservedItemSlotsUnlocked; i++)
            {
                var reservedItemSlot = SessionManager.GetUnlockedReservedItemSlot(i);
                int indexInItemSlotHUD = Array.IndexOf(HUDManager.Instance.itemSlotIconFrames, reservedItemSlots[i]);

                var itemSlotFrame = HUDManager.Instance.itemSlotIconFrames[ReservedPlayerData.localPlayerData.reservedHotbarStartIndex + i];
                var itemSlotIcon = HUDManager.Instance.itemSlotIcons[ReservedPlayerData.localPlayerData.reservedHotbarStartIndex + i];

                //float itemSlotSpacing = GetCurrentItemSlotSpacing();
                itemSlotFrame.rectTransform.sizeDelta = HUDManager.Instance.itemSlotIconFrames[0].rectTransform.sizeDelta;
                itemSlotIcon.rectTransform.sizeDelta = HUDManager.Instance.itemSlotIcons[0].rectTransform.sizeDelta;
                if (HotbarPlus_Compat.Enabled && !ConfigSettings.applyHotbarPlusItemSlotSize.Value)
                {
                    itemSlotFrame.rectTransform.sizeDelta = defaultItemSlotSize;
                    itemSlotIcon.rectTransform.sizeDelta = defaultItemIconSize;
                }
                itemSlotWidth = itemSlotFrame.rectTransform.sizeDelta.x;
                itemSlotSpacing = defaultItemSlotSpacing * currentItemSlotScale;

                var reservedGrabbableObject = ReservedPlayerData.localPlayerData.GetReservedItem(reservedItemSlot);

                itemSlotFrame.name = "Slot" + i + " [ReservedItemSlot] (" + reservedItemSlot.slotName + ")";
                Vector2 hudPosition = new Vector2(defaultItemSlotPosX, defaultItemSlotPosY);

                if (reservedItemSlot.slotPriority >= 0 || !ConfigSettings.displayNegativePrioritySlotsLeftSideOfScreen.Value)
                {
                    hudPosition.x = defaultItemSlotPosX + (defaultItemSlotSize.x - itemSlotWidth) / 2f;
                    hudPosition.y = defaultItemSlotPosY + 36 * (((itemSlotWidth / defaultItemSlotSize.x) - 1) / 2f) + itemSlotSpacing * positiveIndex;
                    if (!ConfigSettings.hideEmptyReservedItemSlots.Value || reservedGrabbableObject != null)
                    {
                        if (!tooltipParent)
                            tooltipParent = itemSlotFrame.rectTransform;
                        positiveIndex++;
                    }
                    else
                        hudPosition.y = -1000;
                }
                else
                {
                    hudPosition.x = -defaultItemSlotPosX - (defaultItemSlotSize.x - itemSlotWidth) / 2f;
                    hudPosition.y = defaultItemSlotPosY + 36 * (((itemSlotWidth / defaultItemSlotSize.x) - 1) / 2f) + itemSlotSpacing * negativeIndex;
                    if (!ConfigSettings.hideEmptyReservedItemSlots.Value || reservedGrabbableObject != null)
                        negativeIndex++;
                    else
                        hudPosition.y = -1000;
                }

                itemSlotFrame.rectTransform.anchoredPosition = hudPosition;

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


            if (SessionManager.numReservedItemSlotsUnlocked > 0 && !ConfigSettings.hideFocusHotbarTooltip.Value)
            {
                if (hotkeyTooltip == null)
                    hotkeyTooltip = new GameObject("ReservedItemSlotTooltip", new Type[] { typeof(RectTransform), typeof(TextMeshProUGUI) }).GetComponent<TextMeshProUGUI>();

                RectTransform tooltipTransform = hotkeyTooltip.rectTransform;
                tooltipTransform.parent = tooltipParent;
                if (tooltipParent)
                {
                    tooltipTransform.localScale = Vector3.one;
                    tooltipTransform.sizeDelta = new Vector2(tooltipParent.sizeDelta.x * 2, 10);
                    tooltipTransform.pivot = Vector2.one / 2;
                    tooltipTransform.anchoredPosition3D = new Vector3(0, -(tooltipTransform.sizeDelta.x / 2) * 1.2f, 0);
                    hotkeyTooltip.font = HUDManager.Instance.controlTipLines[0].font;
                    hotkeyTooltip.fontSize = 7 * (tooltipParent.sizeDelta.x / defaultItemSlotSize.x);
                    hotkeyTooltip.alignment = TextAlignmentOptions.Center;
                    UpdateHotkeyTooltipText();
                }
                else
                    tooltipTransform.localScale = Vector3.zero;
            }

            currentApplyHotbarPlusSize = ConfigSettings.applyHotbarPlusItemSlotSize.Value;
            currentHideEmptySlots = ConfigSettings.hideEmptyReservedItemSlots.Value;
        }


        public static void UpdateHotkeyTooltipText()
        {
            if (localPlayerController == null || hotkeyTooltip == null || Keybinds.FocusReservedHotbarAction == null)
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


        private static float GetCurrentItemSlotSpacing()
        {
            try
            {
                Image frame0 = HUDManager.Instance.itemSlotIconFrames[0];
                Image frame1 = HUDManager.Instance.itemSlotIconFrames[1];
                if (frame0.name.ToLower().Contains("reserved") || frame1.name.ToLower().Contains("reserved"))
                    return defaultItemSlotSpacing;

                return Mathf.Abs(frame1.rectTransform.anchoredPosition.x - frame0.rectTransform.anchoredPosition.x);
            }
            catch { }

            return defaultItemSlotSpacing;
        }


        [HarmonyPatch(typeof(QuickMenuManager), "CloseQuickMenu")]
        [HarmonyPostfix]
        public static void OnCloseQuickMenu()
        {
            if (HotbarPlus_Compat.Enabled || currentHideEmptySlots != ConfigSettings.hideEmptyReservedItemSlots.Value)
                UpdateUI();
        }
    }
}