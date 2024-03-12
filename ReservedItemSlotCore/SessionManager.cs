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

namespace ReservedItemSlotCore
{
    [HarmonyPatch]
    public static class SessionManager
    {
        public static List<ReservedItemSlotData> unlockedReservedItemSlots = new List<ReservedItemSlotData>();
        public static Dictionary<string, ReservedItemSlotData> unlockedReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

        public static Dictionary<string, ReservedItemData> allReservedItemData = new Dictionary<string, ReservedItemData>();

        public static List<ReservedItemSlotData> pendingUnlockedReservedItemSlots = new List<ReservedItemSlotData>();

        public static int numReservedItemSlotsUnlocked { get { return unlockedReservedItemSlots != null ? unlockedReservedItemSlots.Count : 0; } }

        public static bool preGame = true;


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void InitSession()
        {
            unlockedReservedItemSlots.Clear();
            unlockedReservedItemSlotsDict.Clear();
            pendingUnlockedReservedItemSlots.Clear();
            allReservedItemData.Clear();
            preGame = true;
        }


        [HarmonyPatch(typeof(StartOfRound), "ResetPlayersLoadedValueClientRpc")]
        [HarmonyPrefix]
        public static void OnStartGame()
        {
            preGame = false;

            foreach (var reservedItemSlot in SyncManager.unlockableReservedItemSlots)
            {
                if (reservedItemSlot.purchasePrice <= 0)
                    UnlockReservedItemSlot(reservedItemSlot);
            }

            if (pendingUnlockedReservedItemSlots != null)
            {
                foreach (var reservedItemSlot in pendingUnlockedReservedItemSlots)
                    UnlockReservedItemSlot(reservedItemSlot);
                pendingUnlockedReservedItemSlots.Clear();
            }
        }


        public static void UnlockReservedItemSlot(ReservedItemSlotData itemSlotData)
        {
            if (preGame)
            {
                if (!pendingUnlockedReservedItemSlots.Contains(itemSlotData))
                    pendingUnlockedReservedItemSlots.Add(itemSlotData);
                return;
            }

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
                    int hotbarIndex = playerData.reservedHotbarStartIndex + insertIndex;
                    List<GrabbableObject> newItemSlots = new List<GrabbableObject>(playerData.itemSlots);
                    newItemSlots.Insert(hotbarIndex, null);
                    playerData.playerController.ItemSlots = newItemSlots.ToArray();
                }
            }
            if (!unlockedReservedItemSlotsDict.ContainsKey(itemSlotData.slotName))
                unlockedReservedItemSlotsDict.Add(itemSlotData.slotName, itemSlotData);

            UpdateReservedItemsList();
            HUDPatcher.OnUpdateReservedItemSlots();
        }


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
        public static void OnResetShip()
        {
            preGame = true;
            ResetProgress();
        }


        [HarmonyPatch(typeof(GameNetworkManager), "SaveGameValues")]
        [HarmonyPostfix]
        public static void OnSaveGameValues()
        {
            if (NetworkManager.Singleton.IsHost && StartOfRound.Instance.inShipPhase)
                SaveGameValues();
        }


        [HarmonyPatch(typeof(StartOfRound), "LoadUnlockables")]
        [HarmonyPostfix]
        public static void OnLoadGameValues()
        {
            if (NetworkManager.Singleton.IsHost)
                LoadGameValues();
        }


        public static void ResetProgress()
        {
            foreach (var playerData in ReservedPlayerData.allPlayerData.Values)
            {
                var itemSlots = playerData.playerController.ItemSlots;
                List<GrabbableObject> newItemSlots = new List<GrabbableObject>();
                for (int i = 0; i < itemSlots.Length; i++)
                {
                    if (i < playerData.reservedHotbarStartIndex || i >= playerData.reservedHotbarEndIndexExcluded)
                        newItemSlots.Add(itemSlots[i]);
                }
                playerData.playerController.ItemSlots = newItemSlots.ToArray();
            }
            HUDPatcher.OnUpdateReservedItemSlots();

            pendingUnlockedReservedItemSlots?.Clear();
            unlockedReservedItemSlots?.Clear();
            ES3.DeleteKey("ReservedItemSlots.UnlockedItemSlots", GameNetworkManager.Instance.currentSaveFileName);
        }


        public static void SaveGameValues()
        {
            List<string> unlockedItemSlots = new List<string>();
            foreach(var itemSlot in unlockedReservedItemSlots)
            {
                if (!unlockedItemSlots.Contains(itemSlot.slotName))
                    unlockedItemSlots.Add(itemSlot.slotName);
            }
            foreach (var itemSlot in pendingUnlockedReservedItemSlots)
            {
                if (!unlockedItemSlots.Contains(itemSlot.slotName))
                    unlockedItemSlots.Add(itemSlot.slotName);
            }

            Plugin.LogWarning("Saving " + unlockedItemSlots.Count + " unlocked reserved item slots.");
            var unlockedItemSlotsArray = unlockedItemSlots.ToArray();
            ES3.Save("ReservedItemSlots.UnlockedItemSlots", unlockedItemSlotsArray, GameNetworkManager.Instance.currentSaveFileName);
        }


        public static void LoadGameValues()
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


        public static bool IsReservedItem(GrabbableObject grabbableObject) => grabbableObject?.itemProperties != null ? IsReservedItem(grabbableObject.itemProperties.itemName) : false;
        public static bool IsReservedItem(Item item) => item != null ? IsReservedItem(item.itemName) : false;
        public static bool IsReservedItem(string itemName) => allReservedItemData.ContainsKey(itemName);


        public static bool TryGetUnlockedReservedItemData(string itemName, out ReservedItemData itemSlotData) { itemSlotData = null; if (allReservedItemData.TryGetValue(itemName, out var itemData) && itemData.HasUnlockedParentSlot()) itemSlotData = itemData; return itemSlotData != null; }
        public static bool TryGetUnlockedReservedItemData(GrabbableObject item, out ReservedItemData itemSlotData) { itemSlotData = null; return item?.itemProperties != null && TryGetUnlockedReservedItemData(item.itemProperties.itemName, out itemSlotData); }


        public static bool TryGetReservedItemData(string itemName, out ReservedItemData itemSlotData) { itemSlotData = null; if (allReservedItemData.TryGetValue(itemName, out var itemData)) itemSlotData = itemData; return itemSlotData != null; }
        public static bool TryGetReservedItemData(GrabbableObject item, out ReservedItemData itemSlotData) { itemSlotData = null; return item?.itemProperties != null && TryGetReservedItemData(item.itemProperties.itemName, out itemSlotData); }


        public static bool HasReservedItemSlot(GrabbableObject grabbableObject) => grabbableObject?.itemProperties != null ? HasReservedItemSlot(grabbableObject.itemProperties.itemName) : false;
        public static bool HasReservedItemSlot(Item item) => item != null ? HasReservedItemSlot(item.itemName) : false;
        public static bool HasReservedItemSlot(string itemName)
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
    }
}
