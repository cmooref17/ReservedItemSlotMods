using HarmonyLib;
using ReservedItemSlotCore.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using ReservedItemSlotCore.Data;
using ReservedItemSlotCore.Networking;
using Unity.Netcode;
using UnityEngine.Diagnostics;
using GameNetcodeStuff;

namespace ReservedItemSlotCore
{
    [HarmonyPatch]
    public static class SessionManager
    {
        internal static List<ReservedItemSlotData> unlockedReservedItemSlots = new List<ReservedItemSlotData>();
        internal static Dictionary<string, ReservedItemSlotData> unlockedReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();
        private static List<ReservedItemSlotData> pendingUnlockedReservedItemSlots = new List<ReservedItemSlotData>();
        private static Dictionary<string, ReservedItemSlotData> pendingUnlockedReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

        private static Dictionary<string, ReservedItemData> allReservedItemData = new Dictionary<string, ReservedItemData>();

        public static int numReservedItemSlotsUnlocked { get { return unlockedReservedItemSlots != null ? unlockedReservedItemSlots.Count : 0; } }


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        private static void InitSession()
        {
            unlockedReservedItemSlots.Clear();
            unlockedReservedItemSlotsDict.Clear();
            pendingUnlockedReservedItemSlots.Clear();
            pendingUnlockedReservedItemSlotsDict.Clear();
            allReservedItemData.Clear();
        }


        public static void UnlockReservedItemSlot(ReservedItemSlotData itemSlotData)
        {
            Plugin.LogWarning("Unlocking reserved item slot: " + itemSlotData.slotName);
            if (!SyncManager.isSynced)
            {
                if (!pendingUnlockedReservedItemSlotsDict.ContainsKey(itemSlotData.slotName))
                {
                    pendingUnlockedReservedItemSlotsDict.Add(itemSlotData.slotName, itemSlotData);
                    pendingUnlockedReservedItemSlots.Add(itemSlotData);
                }
                return;
            }


            if (!unlockedReservedItemSlotsDict.ContainsKey(itemSlotData.slotName))
            {
                unlockedReservedItemSlotsDict.Add(itemSlotData.slotName, itemSlotData);
                if (!unlockedReservedItemSlots.Contains(itemSlotData))
                {
                    int insertIndex = -1;
                    for (int i = 0; i < unlockedReservedItemSlots.Count; i++)
                    {
                        if (itemSlotData.slotPriority > unlockedReservedItemSlots[i].slotPriority)
                        {
                            insertIndex = i;
                            break;
                        }
                    }

                    if (insertIndex == -1)
                        insertIndex = unlockedReservedItemSlots.Count;

                    unlockedReservedItemSlots.Insert(insertIndex, itemSlotData);

                    foreach (var playerData in ReservedPlayerData.allPlayerData.Values)
                    {
                        if (unlockedReservedItemSlots.Count == 1)
                            playerData.reservedHotbarStartIndex = playerData.itemSlots.Length;

                        int hotbarIndex = playerData.reservedHotbarStartIndex + insertIndex;
                        List<GrabbableObject> newItemSlots = new List<GrabbableObject>(playerData.itemSlots);
                        newItemSlots.Insert(hotbarIndex, null);
                        playerData.playerController.ItemSlots = newItemSlots.ToArray();
                        playerData.hotbarSize = newItemSlots.Count;
                    }
                }
            }

            UpdateReservedItemsList();
            HUDPatcher.OnUpdateReservedItemSlots();
        }


        internal static void UnlockAllPendingItemSlots()
        {
            foreach (var itemSlotData in pendingUnlockedReservedItemSlots)
                UnlockReservedItemSlot(itemSlotData);

            pendingUnlockedReservedItemSlots.Clear();
            pendingUnlockedReservedItemSlotsDict.Clear();
        }


        public static ReservedItemSlotData GetUnlockedReservedItemSlot(int index)
        {
            return unlockedReservedItemSlots != null && index >= 0 && index < unlockedReservedItemSlots.Count ? unlockedReservedItemSlots[index] : null;
        }


        public static ReservedItemSlotData GetUnlockedReservedItemSlot(string itemSlotName)
        {
            if (TryGetUnlockedItemSlotData(itemSlotName, out var itemSlotData))
                return itemSlotData;
            return null;
        }


        public static bool IsItemSlotUnlocked(ReservedItemSlotData itemSlotData) => itemSlotData != null ? unlockedReservedItemSlotsDict.ContainsKey(itemSlotData.slotName) : false;
        public static bool IsItemSlotUnlocked(string itemSlotName) => unlockedReservedItemSlotsDict.ContainsKey(itemSlotName); // || pendingUnlockedReservedItemSlotsDict.ContainsKey(itemSlotName);


        public static void UpdateReservedItemsList()
        {
            if (unlockedReservedItemSlots == null)
                return;

            allReservedItemData.Clear();

            foreach (var itemSlotData in unlockedReservedItemSlots)
            {
                if (itemSlotData.reservedItemData != null)
                {
                    foreach (var itemData in itemSlotData.reservedItemData.Values)
                    {
                        if (!allReservedItemData.ContainsKey(itemData.itemName))
                            allReservedItemData.Add(itemData.itemName, itemData);
                    }
                }
            }
        }


        [HarmonyPatch(typeof(StartOfRound), "ResetShip")]
        [HarmonyPostfix]
        private static void OnResetShip()
        {
            //preGame = true;
            //HUDPatcher.preGameReminderText.enabled = true;
            if (SyncManager.enablePurchasingItemSlots)
                ResetProgress();
        }


        [HarmonyPatch(typeof(GameNetworkManager), "SaveGameValues")]
        [HarmonyPostfix]
        private static void OnSaveGameValues()
        {
            if (NetworkManager.Singleton.IsHost && StartOfRound.Instance.inShipPhase && SyncManager.enablePurchasingItemSlots)
                SaveGameValues();
        }


        [HarmonyPatch(typeof(StartOfRound), "LoadUnlockables")]
        [HarmonyPostfix]
        private static void OnLoadGameValues()
        {
            if (NetworkManager.Singleton.IsHost && SyncManager.isSynced && SyncManager.enablePurchasingItemSlots)
                LoadGameValues();
        }


        internal static void ResetProgress()
        {
            if (!SyncManager.enablePurchasingItemSlots)
                return;

            foreach (var playerData in ReservedPlayerData.allPlayerData.Values)
            {
                PlayerPatcher.SwitchToItemSlot(playerData.playerController, 0);
                var itemSlots = playerData.playerController.ItemSlots;
                List<GrabbableObject> newItemSlots = new List<GrabbableObject>();
                for (int i = 0; i < itemSlots.Length; i++)
                {
                    if (i < playerData.reservedHotbarStartIndex || i >= playerData.reservedHotbarEndIndexExcluded)
                        newItemSlots.Add(itemSlots[i]);
                }
                playerData.playerController.ItemSlots = newItemSlots.ToArray();
            }

            pendingUnlockedReservedItemSlots?.Clear();
            unlockedReservedItemSlots?.Clear();
            unlockedReservedItemSlotsDict?.Clear();

            var itemSlotFrames = new List<Image>(HUDManager.Instance.itemSlotIconFrames);
            var itemSlotIcons = new List<Image>(HUDManager.Instance.itemSlotIcons);

            List<Image> newItemSlotFrames = new List<Image>();
            List<Image> newItemSlotIcons = new List<Image>();

            for (int i = 0; i < HUDManager.Instance.itemSlotIconFrames.Length; i++)
            {
                var frame = HUDManager.Instance.itemSlotIconFrames[i];
                var icon = HUDManager.Instance.itemSlotIcons[i];
                if (!HUDPatcher.reservedItemSlots.Contains(frame))
                {
                    newItemSlotFrames.Add(frame);
                    newItemSlotIcons.Add(icon);
                }
                else
                    GameObject.Destroy(frame.gameObject);
            }
            HUDPatcher.reservedItemSlots.Clear();
            HUDManager.Instance.itemSlotIconFrames = newItemSlotFrames.ToArray();
            HUDManager.Instance.itemSlotIcons = newItemSlotIcons.ToArray();

            HUDPatcher.OnUpdateReservedItemSlots();

            ES3.DeleteKey("ReservedItemSlots.UnlockedItemSlots", GameNetworkManager.Instance.currentSaveFileName);
        }


        internal static void SaveGameValues()
        {
            List<string> unlockedItemSlots = new List<string>();
            foreach(var itemSlot in unlockedReservedItemSlots)
            {
                if (!unlockedItemSlots.Contains(itemSlot.slotName))
                    unlockedItemSlots.Add(itemSlot.slotName);
            }
            /*
            foreach (var itemSlot in pendingUnlockedReservedItemSlots)
            {
                if (!unlockedItemSlots.Contains(itemSlot.slotName))
                    unlockedItemSlots.Add(itemSlot.slotName);
            }
            */

            Plugin.LogWarning("Saving " + unlockedItemSlots.Count + " unlocked reserved item slots.");
            var unlockedItemSlotsArray = unlockedItemSlots.ToArray();
            ES3.Save("ReservedItemSlots.UnlockedItemSlots", unlockedItemSlotsArray, GameNetworkManager.Instance.currentSaveFileName);
        }


        internal static void LoadGameValues()
        {
            string[] unlockedItemSlots = ES3.Load("ReservedItemSlots.UnlockedItemSlots", GameNetworkManager.Instance.currentSaveFileName, new string[0]);
            Plugin.LogWarning("Loading " + unlockedItemSlots.Length + " unlocked reserved item slots.");
            foreach (var itemSlotName in unlockedItemSlots)
            {
                if (SyncManager.unlockableReservedItemSlotsDict.TryGetValue(itemSlotName, out var reservedItemSlot))
                {
                    UnlockReservedItemSlot(reservedItemSlot);
                    SyncManager.SendUnlockItemSlotToClients(reservedItemSlot.slotId);
                }
            }
        }


        public static bool IsReservedItem(GrabbableObject grabbableObject) { string originalItemName = ItemNameMap.GetItemName(grabbableObject); return IsReservedItem(originalItemName) || (grabbableObject?.itemProperties != null && IsReservedItem(grabbableObject.itemProperties.itemName)); } // return grabbableObject?.itemProperties != null ? IsReservedItem(grabbableObject.itemProperties.itemName) : false; }
        public static bool IsReservedItem(Item item) => item != null ? IsReservedItem(item.itemName) : false;
        private static bool IsReservedItem(string itemName) { return allReservedItemData.ContainsKey(itemName); }


        public static bool TryGetUnlockedItemSlotData(string itemSlotName, out ReservedItemSlotData itemSlotData) { itemSlotData = null; unlockedReservedItemSlotsDict.TryGetValue(itemSlotName, out itemSlotData); return itemSlotData != null; }

        //public static bool TryGetUnlockedReservedItemData(string itemName, out ReservedItemData itemData) { itemData = null; if (allReservedItemData.TryGetValue(itemName, out var thisItemData) && thisItemData.HasUnlockedParentSlot()) thisItemData = thisItemData; return thisItemData != null; }
        //public static bool TryGetUnlockedReservedItemData(GrabbableObject item, out ReservedItemData itemData) { itemData = null; return item?.itemProperties != null && TryGetUnlockedReservedItemData(item.itemProperties.itemName, out itemData); }


        public static bool TryGetUnlockedItemData(GrabbableObject item, out ReservedItemData itemData) { itemData = null; string originalItemName = ItemNameMap.GetItemName(item); return TryGetUnlockedItemData(originalItemName, out itemData) || (item?.itemProperties != null && TryGetUnlockedItemData(item.itemProperties.itemName, out itemData)); }
        public static bool TryGetUnlockedItemData(string itemName, out ReservedItemData itemData) { itemData = null; return allReservedItemData.TryGetValue(itemName, out itemData); }


        /*
        public static bool HasReservedItemSlotForItem(GrabbableObject grabbableObject) => grabbableObject?.itemProperties != null ? HasReservedItemSlotForItem(grabbableObject.itemProperties.itemName) : false;
        public static bool HasReservedItemSlotForItem(Item item) => item != null ? HasReservedItemSlotForItem(item.itemName) : false;
        public static bool HasReservedItemSlotForItem(string itemName)
        {
            if (unlockedReservedItemSlots == null)
                return false;

            foreach (var reservedItemSlot in unlockedReservedItemSlots)
            {
                if (reservedItemSlot.ContainsItem(itemName))
                    return true;
            }
            return false;
        }
        */
    }
}
