using GameNetcodeStuff;
using HarmonyLib;
using ReservedItemSlotCore.Networking;
using ReservedItemSlotCore.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace ReservedItemSlotCore.Data
{
    [HarmonyPatch]
    public class ReservedItemSlotData
    {
        internal static Dictionary<string, ReservedItemSlotData> allReservedItemSlotData = new Dictionary<string, ReservedItemSlotData>();
        internal static Dictionary<int, ReservedItemSlotData> allReservedItemSlotDataByPriority = new Dictionary<int, ReservedItemSlotData>();
        internal static Dictionary<string, List<ReservedItemData>> pendingAddReservedItemsToSlots = new Dictionary<string, List<ReservedItemData>>();

        public Dictionary<string, ReservedItemData> reservedItemData = new Dictionary<string, ReservedItemData>();

        public int slotId { get { return SyncManager.unlockableReservedItemSlots != null ? SyncManager.unlockableReservedItemSlots.IndexOf(this) : -1; } }
        public string slotName = "";
        public string slotDisplayName = "";
        public int slotPriority = 0;
        public int purchasePrice = 200;

        public bool isUnlocked { get { return SessionManager.IsItemSlotUnlocked(this); } }


        /// <summary>
        /// Returns the index of this reserved item slot in the specified player's inventory.
        /// </summary>
        /// <param name="playerController"></param>
        /// <returns></returns>
        public int GetIndexInInventory(PlayerControllerB playerController)
        {
            if (playerController == null || !isUnlocked || !ReservedPlayerData.allPlayerData.TryGetValue(playerController, out var playerData))
                return -1;

            return GetReservedItemSlotIndex() + playerData.reservedHotbarStartIndex;
        }
        internal int GetIndexInInventory(ReservedPlayerData playerData) => GetIndexInInventory(playerData?.playerController);


        /// <summary>
        /// Returns the currently held grabbable object in the reserved item slot by the specified player.
        /// </summary>
        /// <param name="playerController"></param>
        /// <returns></returns>
        public GrabbableObject GetHeldObjectInSlot(PlayerControllerB playerController)
        {
            if (playerController == null || !isUnlocked || !ReservedPlayerData.allPlayerData.TryGetValue(playerController, out var playerData))
                return null;

            int indexInInventory = GetIndexInInventory(playerController);
            if (indexInInventory >= playerData.reservedHotbarStartIndex && indexInInventory < playerData.reservedHotbarEndIndexExcluded)
                return playerController.ItemSlots[indexInInventory];

            return null;
        }
        internal GrabbableObject GetHeldObjectInSlot(ReservedPlayerData playerData) => GetHeldObjectInSlot(playerData?.playerController);


        /// <summary>
        /// Returns the item slot frame Image component (HUD element) for this reserved item slot.
        /// </summary>
        /// <returns></returns>
        public Image GetItemSlotFrameHUD()
        {
            if (StartOfRound.Instance?.localPlayerController == null || !isUnlocked)
                return null;

            int reservedItemSlotIndex = GetReservedItemSlotIndex();
            if (reservedItemSlotIndex < 0 || HUDPatcher.reservedItemSlots == null || reservedItemSlotIndex >= HUDPatcher.reservedItemSlots.Count)
                return null;

            return HUDPatcher.reservedItemSlots[reservedItemSlotIndex];
        }


        /// <summary>
        /// Attempts to add an item to an existing reserved item slot by name. If the item slot does not exist with the specified name, nothing will break, but the item will not be added to any reserved item slot.
        /// </summary>
        /// <param name="itemData"></param>
        /// <param name="itemSlotName"></param>
        public static void TryAddItemDataToReservedItemSlot(ReservedItemData itemData, string itemSlotName)
        {
            if (!pendingAddReservedItemsToSlots.ContainsKey(itemSlotName))
                pendingAddReservedItemsToSlots.Add(itemSlotName, new List<ReservedItemData>());

            var reservedItemSlot = pendingAddReservedItemsToSlots[itemSlotName];
            if (reservedItemSlot.Contains(itemData))
                return;

            reservedItemSlot.Add(itemData);
        }


        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPrefix]
        private static void AddPendingItemDataToItemSlots()
        {
            foreach (var kvp in pendingAddReservedItemsToSlots)
            {
                string itemSlotName = kvp.Key;
                var itemDataList = kvp.Value;

                if (!allReservedItemSlotData.TryGetValue(itemSlotName, out var reservedItemSlotData))
                {
                    Plugin.LogWarning("Failed to add item to reserved item slot. Item slot (" + itemSlotName + ") does not exist. This is okay.");
                    continue;
                }
                foreach (var itemData in itemDataList)
                {
                    if (reservedItemSlotData.ContainsItem(itemData.itemName))
                    {
                        Plugin.LogWarning("Failed to add item to reserved item slot. Item already exists in slot. Item: " + itemData.itemName + " Item slot: " + itemSlotName);
                        continue;
                    }
                        reservedItemSlotData.AddItemToReservedItemSlot(itemData);
                }
            }

            pendingAddReservedItemsToSlots.Clear();
        }


        /// <summary>
        /// Creates a new ReservedItemSlotData and adds it to the list of all reserved item slots.
        /// If a slot exists with the same name, but different priority, the name will be slightly adjusted.
        /// If a slot exists with the same priority, but different name, the priority will be lowered by 1.
        /// If a slot exists with the same name and priority, the new slot will not be created, but instead, will return the slot that already exists.
        /// </summary>
        /// <param name="slotName"></param>
        /// <param name="slotPriority"></param>
        /// <param name="purchasePrice"></param>
        /// <returns></returns>
        public static ReservedItemSlotData CreateReservedItemSlotData(string slotName, int slotPriority = 20, int purchasePrice = 200)
        {
            var newSlotData = new ReservedItemSlotData(slotName, slotPriority, purchasePrice);

            bool nameExists = allReservedItemSlotData.TryGetValue(newSlotData.slotName, out var existingSlotDataName);
            bool priorityExists = allReservedItemSlotDataByPriority.TryGetValue(newSlotData.slotPriority, out var existingSlotDataPriority);
            if (nameExists && priorityExists && existingSlotDataName == existingSlotDataPriority)
            {
                Plugin.LogWarning("Attempted to create a duplicate ReservedItemSlotData with the same name and priority as another. SlotName: " + slotName + ". Priority: " + slotPriority + ". ReservedSlot will not be created.");
                return newSlotData;
            }
            else
            {
                if (nameExists)
                {
                    int copyNumber = 0;
                    newSlotData.slotName = slotName + "_" + copyNumber;
                    while (allReservedItemSlotData.ContainsKey(newSlotData.slotName))
                    {
                        copyNumber++;
                        newSlotData.slotName = slotName + "_" + copyNumber;
                    }
                    Plugin.LogWarning("Attempted to create a new ReservedItemSlotData (" + slotName + ") with the same priority as: " + existingSlotDataName.slotName + ". Setting new slot name to: " + newSlotData.slotName);
                }
                if (priorityExists)
                {
                    while (allReservedItemSlotDataByPriority.ContainsKey(newSlotData.slotPriority))
                        newSlotData.slotPriority--;
                    Plugin.LogWarning("Attempted to create a new ReservedItemSlotData (" + slotName + ") with the same priority as: " + existingSlotDataPriority.slotName + ". Adjusting priority to: " + newSlotData.slotPriority);
                }

                allReservedItemSlotData.Add(newSlotData.slotName, newSlotData);
                allReservedItemSlotDataByPriority.Add(newSlotData.slotPriority, newSlotData);

                Plugin.Log("Created ReservedItemSlotData for: " + newSlotData.slotName + ". Slot priority: " + newSlotData.slotPriority);
                return newSlotData;
            }
        }


        internal ReservedItemSlotData(string slotName, int slotPriority, int purchasePrice = 200)
        {
            this.slotName = slotName;
            this.slotPriority = slotPriority;
            this.purchasePrice = purchasePrice;

            slotDisplayName = slotName.Replace('_', ' ').Replace('-', ' ').Trim(' ');
            slotDisplayName = char.ToUpper(slotDisplayName[0]) + slotDisplayName.Substring(1).ToLower();
        }

        /// <summary>
        /// Adds an item to the ReservedItemSlotData.
        /// </summary>
        /// <param name="itemData"></param>
        /// <returns></returns>
        public ReservedItemData AddItemToReservedItemSlot(ReservedItemData itemData)
        {
            if (reservedItemData.ContainsKey(itemData.itemName))
            {
                Plugin.LogWarning("Already added itemData to reserved item slot. Slot: " + slotName + " Item: " + itemData.itemName);
                return null;
            }
            reservedItemData.Add(itemData.itemName, itemData);
            itemData.AddToReservedItemSlot(this);
            return itemData;
        }


        /// <summary>
        /// Removes an item from the ReservedItemSlotData.
        /// </summary>
        /// <param name="itemName"></param>
        public void RemoveItemFromReservedItemSlot(string itemName)
        {
            if (reservedItemData.ContainsKey(itemName))
                reservedItemData.Remove(itemName);
        }


        /// <summary>
        /// Removes an item from the ReservedItemSlotData.
        /// </summary>
        /// <param name="itemData"></param>
        public void RemoveItemFromReservedItemSlot(ReservedItemData itemData)
        {
            if (itemData == null)
                return;

            if (reservedItemData.ContainsKey(itemData.itemName))
                reservedItemData.Remove(itemData.itemName);
        }


        /// <summary>
        /// Returns the index of this reserved item slot in the unlocked item slots list.
        /// This is not the index of the item slot in the player's inventory.
        /// </summary>
        /// <returns></returns>
        public int GetReservedItemSlotIndex()
        {
            if (SessionManager.numReservedItemSlotsUnlocked > 0)
                return SessionManager.unlockedReservedItemSlots.IndexOf(this);
            return -1;
        }


        public bool ContainsItem(GrabbableObject grabbableObject) => grabbableObject?.itemProperties != null && ContainsItem(grabbableObject.itemProperties.itemName);
        public bool ContainsItem(Item item) => item != null && ContainsItem(item.itemName);
        public bool ContainsItem(string itemName) => reservedItemData != null && reservedItemData.ContainsKey(itemName);


        public ReservedItemData GetReservedItemData(GrabbableObject grabbableObject) => grabbableObject?.itemProperties != null ? GetReservedItemData(grabbableObject.itemProperties.itemName) : null;
        public ReservedItemData GetReservedItemData(Item item) => item != null ? GetReservedItemData(item.itemName) : null;
        public ReservedItemData GetReservedItemData(string itemName) => reservedItemData.ContainsKey(itemName) ? reservedItemData[itemName] : null;
    }
}
