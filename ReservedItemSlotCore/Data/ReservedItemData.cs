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
        //internal string actualItemName = "";

        public bool showOnPlayerWhileHolstered { get { return holsteredParentBone != PlayerBone.None; } }
        public PlayerBone holsteredParentBone;
        public Vector3 holsteredPositionOffset = Vector3.zero;
        public Vector3 holsteredRotationOffset = Vector3.zero;


        public ReservedItemData(string itemName, PlayerBone holsteredParentBone = 0, Vector3 holsteredPositionOffset = default, Vector3 holsteredRotationOffset = default)
        {
            this.itemName = itemName; //.ToLower().Replace("_", " ").Replace("-", " ");
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
