using HarmonyLib;
using ReservedItemSlotCore.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public bool slotUnlocked { get { return SessionManager.unlockedReservedItemSlots != null && SessionManager.unlockedReservedItemSlots.Contains(this); } }




        public static void TryAddItemDataToReservedItemSlot(ReservedItemData itemData, string itemSlotName)
        {
            if (!pendingAddReservedItemsToSlots.ContainsKey(itemSlotName))
                pendingAddReservedItemsToSlots.Add(itemSlotName, new List<ReservedItemData>());

            var reservedItemSlot = pendingAddReservedItemsToSlots[itemSlotName];
            if (reservedItemSlot.Contains(itemData))
                return;

            reservedItemSlot.Add(itemData);
            /*
            if (allReservedItemSlotData.TryGetValue(itemSlotName, out var reservedItemSlotData))
            {
                if (reservedItemSlotData.ContainsItem(itemData.itemName))
                {
                    Plugin.LogWarning("Failed to add item to reserved item slot data. Item already exist in slot. Item: " + itemData.itemName + " Slot: " + itemSlotName);
                    return false;
                }
                Plugin.Log("Adding item to existing reserved item slot data: " + itemSlotName + " Item: " + itemData.itemName);
                reservedItemSlotData.AddItemToReservedItemSlot(itemData);
                return true;
            }
            Plugin.LogWarning("Failed to add item to reserved item slot. Could not find reserved item slot with name: " + itemSlotName);
            return false;
            */
        }


        [HarmonyPatch(typeof(PreInitSceneScript), "Awake")]
        [HarmonyPrefix]
        static void AddPendingItemDataToItemSlots()
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



        public static ReservedItemSlotData CreateReservedItemSlotData(string slotName, int slotPriority, int purchasePrice = 200)
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
                    Plugin.LogWarning("Attempted to create a new ReservedItemSlotData (" + slotName + ") with the same priority as: " + existingSlotDataName.slotName + ". Adjusting priority to: " + newSlotData.slotPriority);
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

            slotDisplayName = slotName.Replace('_', ' ').Trim(' ');
            slotDisplayName = char.ToUpper(slotDisplayName[0]) + slotDisplayName.Substring(1).ToLower();
        }


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


        public void RemoveItemFromReservedItemSlot(string itemName)
        {
            if (reservedItemData.ContainsKey(itemName))
                reservedItemData.Remove(itemName);
        }


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
            if (SessionManager.unlockedReservedItemSlots != null)
                return SessionManager.unlockedReservedItemSlots.IndexOf(this);
            return -1;
        }


        public bool ContainsItem(GrabbableObject grabbableObject) => grabbableObject?.itemProperties != null && ContainsItem(grabbableObject.itemProperties.itemName);
        public bool ContainsItem(Item item) => item != null && ContainsItem(item.itemName);
        public bool ContainsItem(string itemName) => reservedItemData != null && reservedItemData.ContainsKey(itemName);


        public ReservedItemData GetReservedItem(GrabbableObject grabbableObject) => grabbableObject?.itemProperties != null ? GetReservedItem(grabbableObject.itemProperties.itemName) : null;
        public ReservedItemData GetReservedItem(Item item) => item != null ? GetReservedItem(item.itemName) : null;
        public ReservedItemData GetReservedItem(string itemName) => reservedItemData.ContainsKey(itemName) ? reservedItemData[itemName] : null;
    }
}
