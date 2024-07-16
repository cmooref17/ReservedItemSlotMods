using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReservedItemSlotCore.Patches;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore;
using ReservedItemSlotCore.Data;
using UnityEngine.UI;
using UnityEngine;
using System.Diagnostics.Eventing.Reader;

/*
namespace ReservedWeaponSlot
{
    internal static class AmmoUI
    {
        private static List<ReservedItemSlotData> currentUnlockedAmmoSlots = new List<ReservedItemSlotData>();
        private static List<Image> currentAmmoSlotFramesUI = new List<Image>();
        private static Dictionary<string, int> ammoCount = new Dictionary<string, int>();
        private static Dictionary<string, Image> ammoReps = new Dictionary<string, Image>();

        [HarmonyPatch(typeof(HUDPatcher), "UpdateUI")]
        [HarmonyPostfix]
        private static void OnUpdateUI()
        {
            if (!SessionManager.IsItemSlotUnlocked("ammo") || !SyncManager.isSynced)
                return;

            ammoCount.Clear();
            ammoReps.Clear();

            int i;
            for (i = 0; ; i++)
            {
                string ammoSlotName = "ammo" + (i > 0 ? i.ToString() : "");
                Plugin.LogWarning("Ammo: " + ammoSlotName);
                var ammoSlot = SessionManager.GetUnlockedReservedItemSlot(ammoSlotName);

                if (ammoSlot == null)
                    break;

                var heldItemInSlot = ammoSlot.GetHeldObjectInSlot(StartOfRound.Instance.localPlayerController);
                var itemSlotFrame = ammoSlot.GetItemSlotFrameHUD();
                itemSlotFrame.gameObject.SetActive(false);

                if (currentUnlockedAmmoSlots == null)
                {
                    currentUnlockedAmmoSlots = new List<ReservedItemSlotData>();
                    currentAmmoSlotFramesUI = new List<Image>();
                }

                if (currentUnlockedAmmoSlots.Count > i)
                {
                    currentUnlockedAmmoSlots[i] = ammoSlot;
                    currentAmmoSlotFramesUI[i] = itemSlotFrame;
                }
                else
                {
                    currentUnlockedAmmoSlots.Add(ammoSlot);
                    currentAmmoSlotFramesUI.Add(itemSlotFrame);
                }

                if (heldItemInSlot)
                {
                    if (!ammoCount.ContainsKey(heldItemInSlot.itemProperties.itemName))
                    {
                        ammoCount.Add(heldItemInSlot.itemProperties.itemName, 0);
                        ammoReps.Add(heldItemInSlot.itemProperties.itemName, itemSlotFrame);
                    }
                    ammoCount[heldItemInSlot.itemProperties.itemName]++;
                }
            }
            for (; i < currentUnlockedAmmoSlots.Count; i++)
                currentUnlockedAmmoSlots[i] = null;

            if (ammoReps.Count <= 0)
            {
                if (currentAmmoSlotFramesUI.Count > 0)
                {
                    currentAmmoSlotFramesUI[0].gameObject.SetActive(true);
                }
            }
            else
            {
                int num = 0;
                foreach (Image ammoSlotRep in ammoReps.Values)
                {
                    ammoSlotRep.gameObject.SetActive(true);
                    Vector2 uiPosition = currentAmmoSlotFramesUI[0].rectTransform.anchoredPosition;
                    uiPosition.y += num * HUDPatcher.itemSlotSpacing;
                    ammoSlotRep.rectTransform.anchoredPosition = uiPosition;
                    num++;
                }
            }
        }
    }
}
*/