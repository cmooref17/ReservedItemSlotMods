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
using ReservedItemSlotCore.Config;
using System.Collections;

namespace ReservedItemSlotCore
{
    [HarmonyPatch]
    public static class SessionManager
    {
        // All unlockable reserved item slots
        internal static List<ReservedItemSlotData> allUnlockableReservedItemSlots { get { return SyncManager.unlockableReservedItemSlots; } }
        internal static Dictionary<string, ReservedItemSlotData> allUnlockableReservedItemSlotsDict { get { return SyncManager.unlockableReservedItemSlotsDict; } }

        // Currently unlocked reserved item slots
        internal static List<ReservedItemSlotData> unlockedReservedItemSlots = new List<ReservedItemSlotData>();
        internal static Dictionary<string, ReservedItemSlotData> unlockedReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

        // Will be unlocked after finished syncing with server
        internal static List<ReservedItemSlotData> pendingUnlockedReservedItemSlots = new List<ReservedItemSlotData>();
        internal static Dictionary<string, ReservedItemSlotData> pendingUnlockedReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

        /// <summary>All reserved item data in currentlyUnlockedSlots</summary>
        private static Dictionary<string, ReservedItemData> allReservedItemData = new Dictionary<string, ReservedItemData>();

        /// <summary>Number of currently unlocked item slots.</summary>
        public static int numReservedItemSlotsUnlocked { get { return unlockedReservedItemSlots != null ? unlockedReservedItemSlots.Count : 0; } }

        internal static bool gameStarted = false;


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        private static void InitSession()
        {
            unlockedReservedItemSlots.Clear();
            unlockedReservedItemSlotsDict.Clear();
            pendingUnlockedReservedItemSlots.Clear();
            pendingUnlockedReservedItemSlotsDict.Clear();
            allReservedItemData.Clear();
            gameStarted = false;
        }


        [HarmonyPatch(typeof(StartOfRound), "ResetPlayersLoadedValueClientRpc")]
        [HarmonyPostfix]
        private static void OnStartGame(StartOfRound __instance, bool landingShip = false)
        {
            if (gameStarted || !NetworkManager.Singleton.IsClient)
                return;

            if (!SyncManager.hostHasMod && SyncManager.canUseModDisabledOnHost)
            {
                Plugin.LogWarning("Starting game while host does not have this mod, and ForceEnableReservedItemSlots is enabled in the config. Unlocking: " + ReservedItemSlotData.allReservedItemSlotData.Count + " slots. THIS MAY NOT BE STABLE");
                SyncManager.isSynced = true;
                SyncManager.enablePurchasingItemSlots = false;
                ReservedPlayerData.localPlayerData.reservedHotbarStartIndex = ReservedPlayerData.localPlayerData.itemSlots.Length;
                foreach (var reservedItemSlotData in ReservedItemSlotData.allReservedItemSlotData.Values)
                {
                    SyncManager.AddReservedItemSlotData(reservedItemSlotData);
                    UnlockReservedItemSlot(reservedItemSlotData);
                }
                pendingUnlockedReservedItemSlots?.Clear();
                pendingUnlockedReservedItemSlotsDict?.Clear();
                SyncManager.UpdateReservedItemsList();
            }
            gameStarted = true;
        }


        /// <summary>
        /// Unlocks the specified reserved item slot for the local client.
        /// </summary>
        /// <param name="itemSlotData"></param>
        public static void UnlockReservedItemSlot(ReservedItemSlotData itemSlotData)
        {
            if (itemSlotData == null)
                return;

            Plugin.Log("Unlocking reserved item slot: " + itemSlotData.slotName);
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
            if (ReservedHotbarManager.indexInReservedHotbar < ReservedPlayerData.localPlayerData.reservedHotbarStartIndex || ReservedHotbarManager.indexInReservedHotbar >= ReservedPlayerData.localPlayerData.reservedHotbarEndIndexExcluded)
                ReservedHotbarManager.indexInReservedHotbar = ReservedPlayerData.localPlayerData.reservedHotbarStartIndex;

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


        /// <summary>
        /// Returns the reserved item slot data for the specified index. The index passed is the index of the reserved item slot in the list of unlocked reserved item slots, and NOT the actual index in the player's inventory.
        /// </summary>
        /// <param name="indexInUnlockedItemSlots"></param>
        /// <returns></returns>
        public static ReservedItemSlotData GetUnlockedReservedItemSlot(int indexInUnlockedItemSlots)
        {
            return unlockedReservedItemSlots != null && indexInUnlockedItemSlots >= 0 && indexInUnlockedItemSlots < unlockedReservedItemSlots.Count ? unlockedReservedItemSlots[indexInUnlockedItemSlots] : null;
        }


        /// <summary>
        /// Returns the reserved item slot with the specified name, if it exists and is unlocked. Otherwise, returns null.
        /// </summary>
        /// <param name="itemSlotName"></param>
        /// <returns></returns>
        public static ReservedItemSlotData GetUnlockedReservedItemSlot(string itemSlotName)
        {
            if (TryGetUnlockedItemSlotData(itemSlotName, out var itemSlotData))
                return itemSlotData;
            return null;
        }


        /// <summary>
        /// Returns true if the reserved item slot exists in the session, and is unlocked.
        /// </summary>
        /// <param name="itemSlotData"></param>
        /// <returns></returns>
        public static bool IsItemSlotUnlocked(ReservedItemSlotData itemSlotData) => itemSlotData != null ? IsItemSlotUnlocked(itemSlotData.slotName) : false;


        /// <summary>
        /// Returns true if the reserved item slot exists in the session, and is unlocked.
        /// </summary>
        /// <param name="itemSlotName"></param>
        /// <returns></returns>
        public static bool IsItemSlotUnlocked(string itemSlotName) => unlockedReservedItemSlotsDict.ContainsKey(itemSlotName); // || pendingUnlockedReservedItemSlotsDict.ContainsKey(itemSlotName);


        internal static void UpdateReservedItemsList()
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
            if (SyncManager.enablePurchasingItemSlots)
                ResetProgressDelayed();
            else if (!SyncManager.hostHasMod && SyncManager.canUseModDisabledOnHost)
            {
                SyncManager.isSynced = false;
                ResetProgressDelayed(true);
            }
            gameStarted = false;
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
            if (NetworkManager.Singleton.IsServer && SyncManager.isSynced && SyncManager.enablePurchasingItemSlots)
                LoadGameValues();
        }


        internal static void ResetProgress(bool force = false)
        {
            if (!SyncManager.enablePurchasingItemSlots && !force)
                return;

            Plugin.Log("Resetting progress.");

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

            unlockedReservedItemSlots?.Clear();
            unlockedReservedItemSlotsDict?.Clear();
            pendingUnlockedReservedItemSlots?.Clear();
            pendingUnlockedReservedItemSlotsDict?.Clear();

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

            foreach (var playerData in ReservedPlayerData.allPlayerData.Values)
            {
                if (playerData.playerController.currentItemSlot < 0 || playerData.playerController.currentItemSlot >= playerData.playerController.ItemSlots.Length)
                    PlayerPatcher.SwitchToItemSlot(playerData.playerController, 0);
                playerData.hotbarSize = playerData.itemSlots.Length;
                playerData.reservedHotbarStartIndex = playerData.hotbarSize;
            }
            foreach (var reservedItemSlot in allUnlockableReservedItemSlots)
            {
                if (reservedItemSlot.purchasePrice <= 0)
                    UnlockReservedItemSlot(reservedItemSlot);
            }
            if (SyncManager.hostHasMod)
            {
            }

            HUDPatcher.OnUpdateReservedItemSlots();

            if (NetworkManager.Singleton.IsServer)
                ES3.DeleteKey("ReservedItemSlots.UnlockedItemSlots", GameNetworkManager.Instance.currentSaveFileName);
        }


        internal static void ResetProgressDelayed(bool force = false)
        {
            IEnumerator Reset()
            {
                yield return null;
                ResetProgress(force);
            }
            StartOfRound.Instance.StartCoroutine(Reset());
        }


        internal static void SaveGameValues()
        {
            if (!NetworkManager.Singleton.IsServer || unlockedReservedItemSlots == null)
                return;

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
            if (!NetworkManager.Singleton.IsServer || SyncManager.unlockableReservedItemSlotsDict == null)
                return;

            string[] unlockedItemSlots = ES3.Load("ReservedItemSlots.UnlockedItemSlots", GameNetworkManager.Instance.currentSaveFileName, new string[0]);
            Plugin.LogWarning("Loading " + unlockedItemSlots.Length + " unlocked reserved item slots.");
            int numItemsLoaded = 0;
            foreach (var itemSlotName in unlockedItemSlots)
            {
                if (SyncManager.unlockableReservedItemSlotsDict.TryGetValue(itemSlotName, out var reservedItemSlot))
                {
                    numItemsLoaded++;
                    UnlockReservedItemSlot(reservedItemSlot);
                    SyncManager.SendUnlockItemSlotToClients(reservedItemSlot.slotId);
                }
            }
            Plugin.Log("Loaded " + numItemsLoaded + " unlocked reserved items.");
        }


        /// <summary>
        /// Returns true if the passed grabbable object is a reserved item, and has a reserved item slot parent that is unlocked.
        /// </summary>
        /// <param name="grabbableObject"></param>
        /// <returns></returns>
        public static bool IsReservedItem(GrabbableObject grabbableObject) { string originalItemName = ItemNameMap.GetItemName(grabbableObject); return IsReservedItem(originalItemName) || (grabbableObject?.itemProperties != null && IsReservedItem(grabbableObject.itemProperties.itemName)); } // return grabbableObject?.itemProperties != null ? IsReservedItem(grabbableObject.itemProperties.itemName) : false; }
        //public static bool IsReservedItem(Item item) => item != null ? IsReservedItem(item.itemName) : false;


        /// <summary>
        /// Returns true if there exists a reserved item with the specified name, and has a reserved item slot parent that is unlocked.
        /// </summary>
        /// <param name="itemName"></param>
        /// <returns></returns>
        public static bool IsReservedItem(string itemName) { return allReservedItemData.ContainsKey(itemName); }


        /// <summary>
        /// Attempts to get the reserved item slot with the specified item slot name. If found, returns true and sets the out parameter to the reserved item slot data. Otherwise, returns false.
        /// </summary>
        /// <param name="itemSlotName"></param>
        /// <param name="itemSlotData"></param>
        /// <returns></returns>
        public static bool TryGetUnlockedItemSlotData(string itemSlotName, out ReservedItemSlotData itemSlotData) { itemSlotData = null; unlockedReservedItemSlotsDict.TryGetValue(itemSlotName, out itemSlotData); return itemSlotData != null; }


        /// <summary>
        /// Attempts to get a reserved item data for the passed grabbable object, if a reserved item data exists, and as a parent reserved item slot that is unlocked.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="itemData"></param>
        /// <returns></returns>
        public static bool TryGetUnlockedItemData(GrabbableObject item, out ReservedItemData itemData) { itemData = null; string originalItemName = ItemNameMap.GetItemName(item); return TryGetUnlockedItemData(originalItemName, out itemData) || (item?.itemProperties != null && TryGetUnlockedItemData(item.itemProperties.itemName, out itemData)); }


        /// <summary>
        /// Attempts to get a reserved item data by name, if a reserved item data exists, and as a parent reserved item slot that is unlocked.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="itemData"></param>
        /// <returns></returns>
        public static bool TryGetUnlockedItemData(string itemName, out ReservedItemData itemData) { itemData = null; return allReservedItemData.TryGetValue(itemName, out itemData); }
    }
}
