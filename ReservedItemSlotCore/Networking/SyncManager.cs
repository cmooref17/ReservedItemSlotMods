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
using System;


namespace ReservedItemSlotCore.Networking
{
    [HarmonyPatch]
    internal static class SyncManager
    {
        public static PlayerControllerB localPlayerController;
        public static bool isSynced = false;
        public static bool purchaseReservedSlotsEnabled = false;
        public static bool canUseModDisabledOnHost { get { return ConfigSettings.forceEnableThisModIfNotEnabledOnHost.Value; } }

        public static List<ReservedItemSlotData> unlockableReservedItemSlots = new List<ReservedItemSlotData>();
        public static Dictionary<string, ReservedItemSlotData> unlockableReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

        public static List<ReservedItemData> reservedItems = new List<ReservedItemData>();
        public static Dictionary<string, ReservedItemData> reservedItemsDict = new Dictionary<string, ReservedItemData>();
        

        public static bool IsReservedItem(string itemSlotName)
        {
            foreach (var itemSlot in unlockableReservedItemSlots)
            {
                if (itemSlot.ContainsItem(itemSlotName))
                    return true;
            }
            return false;
        }


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

                Plugin.LogWarning("purchaseReservedSlotsEnabled: " + purchaseReservedSlotsEnabled);

                //unlockableReservedItemSlotsDict = ReservedItemSlotData.allReservedItemSlotData; // reservedItemsDict;
                //unlockableReservedItemSlots = unlockableReservedItemSlotsDict.Values.ToList();

                unlockableReservedItemSlots = new List<ReservedItemSlotData>();
                unlockableReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

                foreach (var reservedItemSlot in ReservedItemSlotData.allReservedItemSlotData.Values)
                {
                    var newReservedItemSlot = new ReservedItemSlotData(reservedItemSlot.slotName, reservedItemSlot.slotPriority, (int)(reservedItemSlot.purchasePrice * ConfigSettings.globalItemSlotPriceModifier.Value));
                    foreach (var itemData in reservedItemSlot.reservedItemData.Values)
                        newReservedItemSlot.AddItemToReservedItemSlot(new ReservedItemData(itemData.itemName, itemData.holsteredParentBone, itemData.holsteredPositionOffset, itemData.holsteredRotationOffset));
                    
                    AddReservedItemSlotData(newReservedItemSlot);

                    if (!purchaseReservedSlotsEnabled)
                        SessionManager.UnlockReservedItemSlot(reservedItemSlot);
                }

                SessionManager.LoadGameValues();

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
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.RequestSyncClientRpc", RequestSyncClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnUnlockItemSlotClientRpc", OnUnlockItemSlotClientRpc);

                RequestSyncFromServer();
            }

            UpdateReservedItemsList();
        }


        public static void AddReservedItemSlotData(ReservedItemSlotData itemSlotData)
        {
            if (itemSlotData == null)
                return;

            if (unlockableReservedItemSlotsDict != null && !unlockableReservedItemSlotsDict.ContainsKey(itemSlotData.slotName))
            {
                unlockableReservedItemSlotsDict.Add(itemSlotData.slotName, itemSlotData);
                unlockableReservedItemSlots.Add(itemSlotData);
            }
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
                        if (!reservedItemsDict.ContainsKey(itemData.itemName))
                        {
                            reservedItemsDict.Add(itemData.itemName, itemData);
                            reservedItems.Add(itemData);
                        }
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

            int sizeItemInfos = sizeof(int); // num item slots

            foreach (var itemSlotData in unlockableReservedItemSlots)
            {
                if (itemSlotData != null && itemSlotData.reservedItemData != null && itemSlotData.reservedItemData.Count > 0)
                {
                    sizeItemInfos += sizeof(int);
                    byte[] slotNameBytes = System.Text.Encoding.UTF8.GetBytes(itemSlotData.slotName);
                    sizeItemInfos += slotNameBytes.Length;

                    sizeItemInfos += sizeof(int); // slot priority
                    sizeItemInfos += sizeof(int); // slot price

                    sizeItemInfos += sizeof(int); // itemSlotData.reservedItemData.Count; num items in slot
                    foreach (var itemData in itemSlotData.reservedItemData.Values)
                    {
                        sizeItemInfos += sizeof(int);
                        byte[] itemNameBytes = System.Text.Encoding.UTF8.GetBytes(itemData.itemName);
                        sizeItemInfos += itemNameBytes.Length; // sizeItemInfos += sizeof(int) + sizeof(char) * itemData.itemName.Length; // item name
                        sizeItemInfos += sizeof(int); // holster to bone
                        sizeItemInfos += sizeof(float) * 6; // holster to transform position/rotation
                    }
                    sizeItemInfos += sizeof(bool); // is unlocked
                }
            }

            var writer = new FastBufferWriter(sizeof(bool) + sizeItemInfos, Allocator.Temp);
            writer.WriteValue(purchaseReservedSlotsEnabled); // bool
            writer.WriteValue(unlockableReservedItemSlots.Count); // int

            foreach (var itemSlotData in unlockableReservedItemSlots)
            {
                if (itemSlotData != null && itemSlotData.reservedItemData != null && itemSlotData.reservedItemData.Count > 0)
                {
                    byte[] slotNameBytes = System.Text.Encoding.UTF8.GetBytes(itemSlotData.slotName);
                    writer.WriteValue(slotNameBytes.Length);
                    foreach (byte b in slotNameBytes)
                        writer.WriteValue(b);

                    writer.WriteValue(itemSlotData.slotPriority); // int
                    writer.WriteValue(itemSlotData.purchasePrice); // int

                    writer.WriteValue(itemSlotData.reservedItemData.Count); // int
                    foreach (var itemData in itemSlotData.reservedItemData.Values)
                    {
                        byte[] itemNameBytes = System.Text.Encoding.UTF8.GetBytes(itemData.itemName);
                        writer.WriteValue(itemNameBytes.Length);
                        foreach (byte b in itemNameBytes)
                            writer.WriteValue(b);
                        writer.WriteValue((int)itemData.holsteredParentBone); // int
                        writer.WriteValue(itemData.holsteredPositionOffset.x); // float
                        writer.WriteValue(itemData.holsteredPositionOffset.y); // float
                        writer.WriteValue(itemData.holsteredPositionOffset.z); // float
                        writer.WriteValue(itemData.holsteredRotationOffset.x); // float
                        writer.WriteValue(itemData.holsteredRotationOffset.y); // float
                        writer.WriteValue(itemData.holsteredRotationOffset.z); // float
                    }
                    Plugin.LogWarning("Sending slot to client: " + itemSlotData.slotName + " Unlocked: " + itemSlotData.slotUnlocked);
                    writer.WriteValue(itemSlotData.slotUnlocked); // bool
                }
            }

            if (sendToAllClients)
            {
                Plugin.Log("Sending sync to all clients.");
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("ReservedItemSlotCore.RequestSyncClientRpc", writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            else
            {
                Plugin.Log("Sending sync to client with id: " + clientId);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.RequestSyncClientRpc", clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }


        // ClientRpc
        private static void RequestSyncClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            isSynced = true;

            unlockableReservedItemSlots = new List<ReservedItemSlotData>();
            unlockableReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

            Plugin.Log("Receiving sync from server.");

            reader.ReadValue(out purchaseReservedSlotsEnabled);
            reader.ReadValue(out int numEntries);
            for (int i = 0; i < numEntries; i++)
            {
                reader.ReadValue(out int slotNameBytesLength);

                byte[] slotNameBytes = new byte[slotNameBytesLength];
                reader.ReadBytes(ref slotNameBytes, slotNameBytesLength);
                string slotName = System.Text.Encoding.UTF8.GetString(slotNameBytes);


                reader.ReadValue(out int slotPriority);
                reader.ReadValue(out int purchasePrice);

                var itemSlotData = new ReservedItemSlotData(slotName, slotPriority, purchasePrice);

                reader.ReadValue(out int numItemData);

                for (int j = 0; j < numItemData; j++)
                {
                    reader.ReadValue(out int itemNameBytesLength);

                    byte[] itemNameBytes = new byte[itemNameBytesLength];
                    reader.ReadBytes(ref itemNameBytes, itemNameBytesLength);
                    string itemName = System.Text.Encoding.UTF8.GetString(itemNameBytes);

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

                AddReservedItemSlotData(itemSlotData);

                if (unlockedSlot)
                    SessionManager.UnlockReservedItemSlot(itemSlotData);

                Plugin.Log("Receiving sync for reserved item slot data: - Slot: " + itemSlotData.slotName + " Priority: " + itemSlotData.slotPriority + " Unlocked: " + unlockedSlot);
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

            reader.ReadValue(out int reservedItemSlotId);

            if (reservedItemSlotId >= 0 && unlockableReservedItemSlots != null && reservedItemSlotId < unlockableReservedItemSlots.Count)
            {
                if (NetworkManager.Singleton.IsClient)
                {
                    if (clientId != localPlayerController.actualClientId)
                        Plugin.Log("Receiving unlocked reserved item slot update from client for. Item slot id: " + reservedItemSlotId);
                    var reservedItemSlot = unlockableReservedItemSlots[reservedItemSlotId];
                    SessionManager.UnlockReservedItemSlot(reservedItemSlot);
                }
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