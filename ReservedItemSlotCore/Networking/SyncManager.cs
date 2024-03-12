using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ReservedItemSlotCore.Config;
using ReservedItemSlotCore.Patches;
using ReservedItemSlotCore.Data;
using System.Linq;

namespace ReservedItemSlotCore.Networking
{
    [HarmonyPatch]
    internal class SyncManager
    {
        public static PlayerControllerB localPlayerController;
        public static bool isSynced = false;
        public static bool purchaseReservedSlotsEnabled = false;
        public static bool canUseModDisabledOnHost { get { return ConfigSettings.forceEnableThisModIfNotEnabledOnHost.Value; } }

        public static List<ReservedItemSlotData> unlockableReservedItemSlots = new List<ReservedItemSlotData>();
        public static Dictionary<string, ReservedItemSlotData> unlockableReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

        public static List<ReservedItemData> reservedItems = new List<ReservedItemData>();
        public static Dictionary<string, ReservedItemData> reservedItemsDict = new Dictionary<string, ReservedItemData>();
        //public static List<ReservedItemSlotData> syncReservedItemsList = new List<ReservedItemSlotData>();
        //public static List<ReservedItemSlotData> syncReservedItemSlotReps = new List<ReservedItemSlotData>();

        //public static int numReservedItemSlots { get { return unlockableReservedItemSlots.Count; } }


        public static bool IsReservedItem(string itemSlotName)
        {
            foreach (var itemSlot in unlockableReservedItemSlots)
            {
                if (itemSlot.ContainsItem(itemSlotName))
                    return true;
            }
            return false;
        }
        //public static bool TryGetReservedItemData(string itemName, out ReservedItemSlotData itemSlotData) { itemSlotData = null; if (IsReservedItem(itemName)) { itemSlotData = unlockableReservedItemSlotsDict[itemName]; return true; } return false; }
        //public static bool TryGetReservedItemData(GrabbableObject item, out ReservedItemSlotData itemSlotData) { itemSlotData = null; return item != null && TryGetReservedItemData(item.itemProperties.itemName, out itemSlotData); }



        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void ResetValues()
        {
            isSynced = false;
            localPlayerController = null;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init(PlayerControllerB __instance)
        {
            localPlayerController = __instance;


            if (NetworkManager.Singleton.IsServer)
            {
                isSynced = true;
                purchaseReservedSlotsEnabled = !ConfigSettings.disablePurchasingReservedSlots.Value;
                unlockableReservedItemSlotsDict = ReservedItemSlotData.allReservedItemSlotData; // reservedItemsDict;
                unlockableReservedItemSlots = unlockableReservedItemSlotsDict.Values.ToList();

                foreach (var reservedItemSlot in unlockableReservedItemSlots)
                    SessionManager.UnlockReservedItemSlot(reservedItemSlot);

                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnSwapHotbarServerRpc", OnSwapHotbarServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.RequestSyncServerRpc", RequestSyncServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnUnlockItemSlotServerRpc", OnUnlockItemSlotServerRpc);
            }
            else
            {
                isSynced = false;
                unlockableReservedItemSlotsDict = canUseModDisabledOnHost ? ReservedItemSlotData.allReservedItemSlotData : new Dictionary<string, ReservedItemSlotData>();
                unlockableReservedItemSlots = unlockableReservedItemSlotsDict.Values.ToList();

                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnSwapHotbarClientRpc", OnSwapHotbarClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlots.RequestSyncClientRpc", RequestSyncClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnUnlockItemSlotClientRpc", OnUnlockItemSlotClientRpc);

                RequestSyncFromServer();
            }

            UpdateReservedItemsList();
            SessionManager.LoadGameValues();
        }


        public static void AddReservedItemSlotData(ReservedItemSlotData itemSlotData)
        {
            if (itemSlotData == null)
                return;

            if (unlockableReservedItemSlots != null)
                unlockableReservedItemSlots.Add(itemSlotData);
            if (unlockableReservedItemSlotsDict != null)
                unlockableReservedItemSlotsDict.Add(itemSlotData.slotName, itemSlotData);
        }


        public static void UpdateReservedItemsList()
        {
            if (unlockableReservedItemSlots == null)
                return;

            reservedItems.Clear();
            reservedItemsDict.Clear();

            foreach (var itemSlotData in unlockableReservedItemSlots)
            {
                if (itemSlotData.reservedItemData != null)
                {
                    foreach (var itemData in itemSlotData.reservedItemData.Values)
                    {
                        if (!reservedItems.Contains(itemData))
                            reservedItems.Add(itemData);
                        if (!reservedItemsDict.ContainsKey(itemData.itemName))
                            reservedItemsDict.Add(itemData.itemName, itemData);
                    }
                }
            }
        }


        static void RequestSyncFromServer()
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            Plugin.Log("Requesting sync with server.");
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.RequestSyncServerRpc", NetworkManager.ServerClientId, new FastBufferWriter(0, Allocator.Temp));
            return;
        }


        // ServerRpc
        private static void RequestSyncServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            Plugin.Log("Receiving sync request from client: " + clientId);
            SendSyncToClient(clientId);
        }


        public static void SendSyncToClient(ulong clientId) => SendSync(clientId);
        public static void SendSyncToAllClients() => SendSync(0, true);


        static void SendSync(ulong clientId = 0, bool sendToAllClients = false)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            int sizeItemInfos = sizeof(int) * unlockableReservedItemSlots.Count;
            foreach (var itemSlotData in unlockableReservedItemSlots)
            {
                if (itemSlotData != null && itemSlotData.reservedItemData != null && itemSlotData.reservedItemData.Count > 0)
                {
                    sizeItemInfos += sizeof(int) + sizeof(char) * itemSlotData.slotName.Length; // slot name
                    sizeItemInfos += sizeof(int); // slot priority
                    sizeItemInfos += sizeof(int); // purchase price

                    sizeItemInfos += itemSlotData.reservedItemData.Count; // num items in slot
                    foreach (var itemData in itemSlotData.reservedItemData.Values)
                    {
                        sizeItemInfos += sizeof(int) + sizeof(char) * itemData.itemName.Length; // item name
                        sizeItemInfos += sizeof(int); // holster to bone index
                        sizeItemInfos += sizeof(float) * 6; // holster to transform position/rotation
                    }
                }
                sizeItemInfos += sizeof(bool); // is unlocked
            }

            var writer = new FastBufferWriter(sizeof(bool) + sizeItemInfos, Allocator.Temp);
            writer.WriteValue(purchaseReservedSlotsEnabled);
            writer.WriteValue(unlockableReservedItemSlots.Count);

            foreach (var itemSlotData in unlockableReservedItemSlots)
            {
                writer.WriteValue(itemSlotData.slotName.Length);
                foreach (var c in itemSlotData.slotName)
                    writer.WriteValue(c);
                writer.WriteValue(itemSlotData.slotPriority);
                writer.WriteValue(itemSlotData.purchasePrice);

                foreach (var itemData in itemSlotData.reservedItemData.Values)
                {
                    writer.WriteValue(itemData.itemName.Length);
                    foreach (var c in itemData.itemName)
                        writer.WriteValue(c);
                    writer.WriteValue(itemData.holsteredParentBone);
                    writer.WriteValue(itemData.holsteredPositionOffset.x);
                    writer.WriteValue(itemData.holsteredPositionOffset.y);
                    writer.WriteValue(itemData.holsteredPositionOffset.z);
                    writer.WriteValue(itemData.holsteredRotationOffset.x);
                    writer.WriteValue(itemData.holsteredRotationOffset.y);
                    writer.WriteValue(itemData.holsteredRotationOffset.z);
                }
                writer.WriteValue(itemSlotData.slotUnlocked);
            }

            if (sendToAllClients)
            {
                Plugin.Log("Sending sync to client with id: " + clientId);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.RequestSyncClientRpc", clientId, writer);
            }
            else
            {
                Plugin.Log("Sending sync to all clients.");
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("ReservedItemSlotCore.RequestSyncClientRpc", writer);
            }
        }


        // ClientRpc
        private static void RequestSyncClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            //PlayerPatcher.isSynced = false;
            isSynced = true;

            unlockableReservedItemSlots = new List<ReservedItemSlotData>();
            unlockableReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

            Plugin.Log("Receiving sync from server.");

            reader.ReadValue(out int numEntries);

            for (int i = 0; i < numEntries; i++)
            {
                reader.ReadValue(out int lengthSlotName);
                string slotName = "";
                for (int j = 0; j < lengthSlotName; j++)
                {
                    reader.ReadValue(out char c);
                    slotName += c;
                }

                reader.ReadValue(out int slotPriority);
                reader.ReadValue(out int purchasePrice);

                var itemSlotData = ReservedItemSlotData.CreateReservedItemSlotData(slotName, slotPriority, purchasePrice);

                reader.ReadValue(out int numItemData);

                for (int j = 0; j < numItemData; j++)
                {
                    reader.ReadValue(out int lengthItemName);
                    string itemName = "";
                    for (int k = 0; k < lengthItemName; k++)
                    {
                        reader.ReadValue(out char c);
                        itemName += c;
                    }

                    reader.ReadValue(out int holsteredParentBoneIndex);
                    PlayerBone bone = (PlayerBone)holsteredParentBoneIndex;
                    Vector3 holsteredPositionOffset;
                    Vector3 holsteredRotationOffset;
                    reader.ReadValue(out holsteredPositionOffset.x);
                    reader.ReadValue(out holsteredPositionOffset.y);
                    reader.ReadValue(out holsteredPositionOffset.z);
                    reader.ReadValue(out holsteredRotationOffset.x);
                    reader.ReadValue(out holsteredRotationOffset.y);
                    reader.ReadValue(out holsteredRotationOffset.z);

                    var itemData = new ReservedItemData(itemName, bone, holsteredPositionOffset, holsteredRotationOffset);
                    itemSlotData.AddItemToReservedItemSlot(itemData);
                }
                reader.ReadValue(out bool unlockedSlot);
                if (unlockedSlot)
                    SessionManager.UnlockReservedItemSlot(itemSlotData);

                AddReservedItemSlotData(itemSlotData);
                Plugin.Log("Receiving sync for reserved item slot data: - Slot: " + itemSlotData.slotName + " Priority: " + itemSlotData.slotPriority);
            }

            UpdateReservedItemsList();
            Plugin.Log("Received sync for " + unlockableReservedItemSlotsDict.Count + " reserved item slots.");
        }








        public static void SendUnlockItemSlotUpdateToServer(int reservedItemSlotId)
        {
            var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            Plugin.Log("Sending unlocked reserved item slot update to server. Item slot id: " + reservedItemSlotId);
            writer.WriteValue(reservedItemSlotId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.OnUnlockItemSlotServerRpc", NetworkManager.ServerClientId, writer);
        }


        // ServerRpc
        private static void OnUnlockItemSlotServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;
            if (ConfigSync.instance.disablePurchasingReservedSlots)
                return;

            reader.ReadValue(out int reservedItemSlotId);

            if (reservedItemSlotId >= 0 && unlockableReservedItemSlots != null && reservedItemSlotId < unlockableReservedItemSlots.Count)
            {
                Plugin.Log("Receiving unlocked reserved item slot update from client for. Item slot id: " + reservedItemSlotId);
                SendUnlockItemSlotToClients(reservedItemSlotId);
                return;
            }
            Plugin.LogError("Failed to receive unlock reserved item slot from client. Received item slot id: " + reservedItemSlotId + " Size of unlockable reserved item slots: " + unlockableReservedItemSlots.Count);
        }


        public static void SendUnlockItemSlotToClients(int reservedItemSlotId)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            if (reservedItemSlotId >= 0 && unlockableReservedItemSlots != null && reservedItemSlotId < unlockableReservedItemSlots.Count)
            {
                if (NetworkManager.Singleton.IsClient)
                {
                    var reservedItemSlot = unlockableReservedItemSlots[reservedItemSlotId];
                    SessionManager.UnlockReservedItemSlot(reservedItemSlot);
                }

                var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
                writer.WriteValue(reservedItemSlotId);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("ReservedItemSlotCore.OnUnlockItemSlotClientRpc", writer);
                return;
            }
        }


        // ClientRpc
        private static void OnUnlockItemSlotClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;
            if (ConfigSync.instance.disablePurchasingReservedSlots)
                return;

            reader.ReadValue(out int reservedItemSlotId);

            if (reservedItemSlotId >= 0 && unlockableReservedItemSlots != null && reservedItemSlotId < unlockableReservedItemSlots.Count)
            {
                Plugin.Log("Receiving unlocked reserved item slot update from server. Item slot id: " + reservedItemSlotId);
                var reservedItemSlot = unlockableReservedItemSlots[reservedItemSlotId];
                SessionManager.UnlockReservedItemSlot(reservedItemSlot);
                return;
            }
            Plugin.LogError("Failed to receive unlock reserved item slot from server. Received item slot id: " + reservedItemSlotId + " Size of unlockable reserved item slots: " + unlockableReservedItemSlots.Count);
        }








        static void SendSwapHotbarUpdateToServer(int hotbarSlot)
        {
            if (!NetworkManager.Singleton.IsClient)
                return;
            //Plugin.Log("Sending OnSwapReservedHotbar update to server. Hotbar slot: " + hotbarSlot);
            var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            writer.WriteValue(hotbarSlot);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.OnSwapHotbarServerRpc", NetworkManager.ServerClientId, writer);
        }


        // ServerRpc
        static void OnSwapHotbarServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            reader.ReadValue(out int hotbarIndex);
            if (NetworkManager.Singleton.IsClient && clientId != localPlayerController.actualClientId)
            {
                Plugin.Log("Receiving OnSwapReservedHotbar update from client. ClientId: " + clientId + " Slot: " + hotbarIndex);
                TryUpdateClientHotbarSlot(clientId, hotbarIndex);
            }
            SendSwapHotbarUpdateToClients(clientId, hotbarIndex);
        }


        public static void SendSwapHotbarUpdateToClients(ulong clientId, int hotbarIndex)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            var writer = new FastBufferWriter(sizeof(int) + sizeof(ulong), Allocator.Temp);
            writer.WriteValueSafe(hotbarIndex);
            writer.WriteValueSafe(clientId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("ReservedItemSlotCore.OnSwapHotbarClientRpc", writer);
        }


        // ClientRpc
        static void OnSwapHotbarClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            reader.ReadValue(out int hotbarIndex);
            reader.ReadValue(out ulong swappingClientId);
            Plugin.Log("Receiving OnSwapReservedHotbar update from client. ClientId: " + swappingClientId + " Slot: " + hotbarIndex);
                
            if (swappingClientId == localPlayerController.actualClientId || TryUpdateClientHotbarSlot(swappingClientId, hotbarIndex))
                return;
            Plugin.Log("Failed to receive hotbar swap index from Client: " + swappingClientId);
        }


        static bool TryUpdateClientHotbarSlot(ulong clientId, int hotbarSlot)
        {
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                var playerController = StartOfRound.Instance.allPlayerScripts[i];
                if (playerController.actualClientId == clientId)
                {
                    CallSwitchToItemSlotMethod(playerController, hotbarSlot);
                    return true;
                }
            }
            return false;
        }


        public static void SwapHotbarSlot(int hotbarIndex)
        {
            SendSwapHotbarUpdateToServer(hotbarIndex);
            CallSwitchToItemSlotMethod(localPlayerController, hotbarIndex);
        }


        static void CallSwitchToItemSlotMethod(PlayerControllerB playerController, int hotbarIndex)
        {
            if (playerController == null || playerController.ItemSlots == null || hotbarIndex < 0 || hotbarIndex >= playerController.ItemSlots.Length)
                return;
            if (playerController == localPlayerController)
            {
                ShipBuildModeManager.Instance.CancelBuildMode(true);
                playerController.playerBodyAnimator.SetBool("GrabValidated", value: false);
            }
            PlayerPatcher.SwitchToItemSlot(playerController, hotbarIndex);
            if (playerController.currentlyHeldObjectServer != null)
                playerController.currentlyHeldObjectServer.gameObject.GetComponent<AudioSource>().PlayOneShot(playerController.currentlyHeldObjectServer.itemProperties.grabSFX, 0.6f);
        }
    }
}