using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReservedItemSlotCore.Networking;
using System.Reflection;
using BepInEx.Logging;

namespace ReservedItemSlotCore.Data
{
    [HarmonyPatch]
    public class ReservedPlayerData
    {
        public static Dictionary<PlayerControllerB, ReservedPlayerData> allPlayerData = new Dictionary<PlayerControllerB, ReservedPlayerData>();
        public static ReservedPlayerData localPlayerData { get { return StartOfRound.Instance?.localPlayerController != null && allPlayerData.TryGetValue(StartOfRound.Instance.localPlayerController, out var playerData) ? playerData : null; } }


        public PlayerControllerB playerController;
        public bool isLocalPlayer { get { return playerController != null && playerController == StartOfRound.Instance?.localPlayerController; } }
        public int currentItemSlot { get { return playerController.currentItemSlot; } }
        public GrabbableObject currentlySelectedItem { get { return itemSlots != null && currentItemSlot >= 0 && currentItemSlot < itemSlots.Length ? itemSlots[currentItemSlot] : null; } }
        public bool currentItemSlotIsReserved { get { return currentItemSlot >= reservedHotbarStartIndex && currentItemSlot < reservedHotbarStartIndex + SessionManager.numReservedItemSlotsUnlocked; } }
        public ReservedItemData grabbingReservedItemData = null;
        public ReservedItemSlotData grabbingReservedItemSlotData = null;
        public GrabbableObject grabbingReservedItem = null;
        public bool isGrabbingReservedItem { get { return grabbingReservedItemData != null; } }
        public int previousHotbarIndex = -1;
        public GrabbableObject previouslyHeldItem { get { return previousHotbarIndex >= 0 && previousHotbarIndex < playerController.ItemSlots.Length ? playerController.ItemSlots[previousHotbarIndex] : null; } }

        public bool inReservedHotbarSlots = false;
        public int hotbarSize = 4;
        public int reservedHotbarStartIndex = 4;
        public int reservedHotbarEndIndexExcluded { get { return reservedHotbarStartIndex + SessionManager.numReservedItemSlotsUnlocked; } }

        public GrabbableObject[] itemSlots { get { return playerController?.ItemSlots; } }
        public bool throwingObject { get { return (bool)Traverse.Create(playerController).Field("throwingObject").GetValue(); } }
        public float timeSinceSwitchingSlots { get { return (float)Traverse.Create(playerController).Field("timeSinceSwitchingSlots").GetValue(); } set { Traverse.Create(playerController).Field("timeSinceSwitchingSlots").SetValue(value); } }

        public BoneMap boneMap = new BoneMap();

        public ReservedPlayerData(PlayerControllerB playerController)
        {
            this.playerController = playerController;
            boneMap.CreateBoneMap(playerController.transform.transform);
        }


        public bool IsReservedItemSlot(int itemSlotIndex) => itemSlotIndex >= reservedHotbarStartIndex && itemSlotIndex < reservedHotbarStartIndex + SessionManager.numReservedItemSlotsUnlocked;
        public int GetNumHeldReservedItems()
        {
            int num = 0;
            for (int i = reservedHotbarStartIndex; i < reservedHotbarEndIndexExcluded; i++)
            {
                var item = playerController.ItemSlots[i];
                num += (item != null ? 1 : 0);
            }
            return num;
        }


        public GrabbableObject GetReservedItem(ReservedItemSlotData itemSlotData)
        {
            if (itemSlotData == null)
                return null;
            if (!SessionManager.TryGetUnlockedItemSlotData(itemSlotData.slotName, out itemSlotData))
                return null;

            int indexInInventory = itemSlotData.GetIndexInInventory(this);
            if (indexInInventory < reservedHotbarStartIndex || indexInInventory >= reservedHotbarEndIndexExcluded)
                return null;
            if (indexInInventory < 0 || indexInInventory >= playerController.ItemSlots.Length)
                return null;
            return itemSlots[indexInInventory];
        }


        public bool HasEmptySlotForReservedItem(GrabbableObject grabbableObject) => grabbableObject?.itemProperties != null ? HasEmptySlotForReservedItem(grabbableObject.itemProperties.itemName) : false;


        public bool HasEmptySlotForReservedItem(string itemName)
        {
            if (!SessionManager.TryGetUnlockedItemData(itemName, out var itemData) || !itemData.HasUnlockedParentSlot())
                return false;

            for (int i = 0; i < SessionManager.numReservedItemSlotsUnlocked; i++)
            {
                var reservedItemSlot = SessionManager.GetUnlockedReservedItemSlot(i);
                if (!reservedItemSlot.ContainsItem(itemName))
                    continue;
                int indexInInventory = i + reservedHotbarStartIndex;
                if (indexInInventory >= playerController.ItemSlots.Length)
                    return false;
                if (playerController.ItemSlots[indexInInventory] == null)
                    return true;
            }
            return false;
        }


        public ReservedItemSlotData GetFirstEmptySlotForReservedItem(GrabbableObject grabbableObject) => grabbableObject?.itemProperties != null ? GetFirstEmptySlotForReservedItem(grabbableObject.itemProperties.itemName) : null;


        public ReservedItemSlotData GetFirstEmptySlotForReservedItem(string itemName)
        {
            for (int i = 0; i < SessionManager.numReservedItemSlotsUnlocked; i++)
            {
                var reservedItemSlot = SessionManager.GetUnlockedReservedItemSlot(i);
                if (!reservedItemSlot.ContainsItem(itemName))
                    continue;

                int indexInInventory = i + reservedHotbarStartIndex;
                if (indexInInventory < 0 || indexInInventory >= playerController.ItemSlots.Length)
                    return null;
                if (playerController.ItemSlots[indexInInventory] == null)
                    return reservedItemSlot;
            }
            return null;
        }


        public ReservedItemSlotData GetParentReservedItemSlot(GrabbableObject grabbableObject)
        {
            if (grabbableObject == null) return null;

            for (int i = reservedHotbarStartIndex; i < reservedHotbarEndIndexExcluded; i++)
            {
                var item = itemSlots[i];
                if (grabbableObject == item)
                {
                    int reservedIndex = i - reservedHotbarStartIndex;
                    return SessionManager.GetUnlockedReservedItemSlot(reservedIndex);
                }
            }
            return null;
        }


        public ReservedItemSlotData GetCurrentlySelectedReservedItemSlot()
        {
            if (currentItemSlotIsReserved)
            {
                int reservedIndex = currentItemSlot - reservedHotbarStartIndex;
                if (reservedIndex >= 0 && reservedIndex < SessionManager.numReservedItemSlotsUnlocked)
                    return SessionManager.GetUnlockedReservedItemSlot(reservedIndex);
            }
            return null;
        }


        public bool IsItemInReservedItemSlot(GrabbableObject grabbableObject)
        {
            if (grabbableObject == null)
                return false;

            if (reservedHotbarStartIndex < 0 || (reservedHotbarEndIndexExcluded - 1) >= itemSlots.Length)
            {
                Plugin.LogError("Failed to check if item was in reserved item slot. Start or end reserved item slot index was outside the bounds of the player's item slots. Start index: " + reservedHotbarStartIndex + " EndIndex: " + (reservedHotbarEndIndexExcluded - 1) + " InventorySize: " + itemSlots.Length);
                Plugin.LogError("Reporting this to Flip would be greatly appreciated :)");
                return false;
            }
            for (int i = reservedHotbarStartIndex; i < reservedHotbarEndIndexExcluded; i++)
            {
                if (grabbableObject == itemSlots[i])
                    return true;
            }
            return false;
        }
        //GetParentReservedItemSlot(grabbableObject) != null;



        internal int CallGetNextItemSlot(bool forward)
        {
            MethodInfo method = playerController.GetType().GetMethod("NextItemSlot", BindingFlags.NonPublic | BindingFlags.Instance);
            int index = (int)method.Invoke(playerController, new object[] { forward });
            return index;
        }


        internal void CallSwitchToItemSlot(int index, GrabbableObject fillSlotWithItem = null)
        {
            MethodInfo method = playerController.GetType().GetMethod("SwitchToItemSlot", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(playerController, new object[] { index, fillSlotWithItem });
            timeSinceSwitchingSlots = 0;
        }
    }
}
