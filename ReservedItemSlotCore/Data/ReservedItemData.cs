using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReservedItemSlotCore.Data
{
    public class ReservedItemData
    {
        internal static Dictionary<string, ReservedItemData> allReservedItems = new Dictionary<string, ReservedItemData>();

        public List<ReservedItemSlotData> parentItemSlots = new List<ReservedItemSlotData>();
        public string itemName = "";

        public bool showOnPlayerWhileHolstered { get { return holsteredParentBone != 0; } }
        public PlayerBone holsteredParentBone;
        public Vector3 holsteredPositionOffset = Vector3.zero;
        public Vector3 holsteredRotationOffset = Vector3.zero;


        public static ReservedItemData RegisterReservedItem(string itemName, PlayerBone holsteredParentBone = 0, Vector3 holsteredPositionOffset = default, Vector3 holsteredRotationOffset = default)
        {
            if (allReservedItems.TryGetValue(itemName, out var reservedItemData))
            {
                Plugin.LogWarning("Item: " + itemName + " already registered as a reserved item. Current reserved item data will be unchanged, but new reserved slot parents can be set.");
                return reservedItemData;
            }
            var newReservedItemData = new ReservedItemData(itemName, holsteredParentBone, holsteredPositionOffset, holsteredRotationOffset);
            allReservedItems.Add(itemName, newReservedItemData);
            return newReservedItemData;
        }


        public static void RegisterReservedItem(ReservedItemData reservedItemData)
        {
            if (allReservedItems.ContainsKey(reservedItemData.itemName))
            {
                Plugin.LogWarning("Item: " + reservedItemData.itemName + " already registered as a reserved item. Current reserved item data will be unchanged, but new reserved slot parents can be set.");
                return;
            }
            allReservedItems.Add(reservedItemData.itemName, reservedItemData);
        }


        public ReservedItemData(string itemName, PlayerBone holsteredParentBone = 0, Vector3 holsteredPositionOffset = default, Vector3 holsteredRotationOffset = default)
        {
            this.itemName = itemName;
            this.holsteredParentBone = holsteredParentBone;
            this.holsteredPositionOffset = holsteredPositionOffset;
            this.holsteredRotationOffset = holsteredRotationOffset;
        }


        public void AddToReservedItemSlot(ReservedItemSlotData itemSlotData)
        {
            if (parentItemSlots.Contains(itemSlotData))
            {
                Plugin.LogWarning("Item: " + itemName + " already as reserved item slot: " + itemSlotData.slotName + " set as a parent! Ignoring.");
                return;
            }
            parentItemSlots.Add(itemSlotData);
        }


        public bool HasUnlockedParentSlot()
        {
            if (parentItemSlots != null)
            {
                foreach (var itemSlot in parentItemSlots)
                {
                    if (SessionManager.unlockedReservedItemSlots != null && SessionManager.unlockedReservedItemSlots.Contains(itemSlot))
                        return true;
                }
            }
            return false;
        }
    }
}
