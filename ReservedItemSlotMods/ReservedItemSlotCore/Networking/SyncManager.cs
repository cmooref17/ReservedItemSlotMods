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
using System.Collections;


namespace ReservedItemSlotCore.Networking
{
    [HarmonyPatch]
    internal static class SyncManager
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        public static bool enablePurchasingItemSlots { get; internal set; } = false;
        internal static bool canUseModDisabledOnHost { get { return ConfigSettings.forceEnableThisModIfNotEnabledOnHost.Value; } }

        public static bool isSynced { get; internal set; } = false;
        public static bool hostHasMod { get; private set; } = false;
        private static bool requestedSyncHeldObjects = false;

        public static List<ReservedItemSlotData> unlockableReservedItemSlots = new List<ReservedItemSlotData>();
        public static Dictionary<string, ReservedItemSlotData> unlockableReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

        public static List<ReservedItemData> reservedItems = new List<ReservedItemData>();
        public static Dictionary<string, ReservedItemData> reservedItemsDict = new Dictionary<string, ReservedItemData>();
        

        public static bool IsReservedItem(string itemName)
        {
            if (itemName == null)
                return false;

            foreach (var itemSlot in unlockableReservedItemSlots)
            {
                if (itemSlot.ContainsItem(itemName))
                    return true;
            }
            return false;
        }


        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void ResetValues(StartOfRound __instance)
        {
            isSynced = false;
            hostHasMod = false;
            requestedSyncHeldObjects = false;
            unlockableReservedItemSlots?.Clear();
            unlockableReservedItemSlotsDict?.Clear();
            enablePurchasingItemSlots = false;
            reservedItems?.Clear();
            reservedItemsDict?.Clear();
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init(PlayerControllerB __instance)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                enablePurchasingItemSlots = ConfigSettings.enablePurchasingItemSlots.Value;
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

                    if (!enablePurchasingItemSlots)
                        SessionManager.UnlockReservedItemSlot(newReservedItemSlot);
                }

                if (enablePurchasingItemSlots)
                    SessionManager.LoadGameValues();

                if (enablePurchasingItemSlots)
                {
                    foreach (var reservedItemSlot in unlockableReservedItemSlots)
                    {
                        if (!reservedItemSlot.isUnlocked && reservedItemSlot.purchasePrice <= 0)
                            SessionManager.UnlockReservedItemSlot(reservedItemSlot);
                    }
                }

                isSynced = true;
                hostHasMod = true;
                OnSyncedWithServer();

                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnSwapHotbarServerRpc", OnSwapHotbarServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.RequestSyncServerRpc", RequestSyncServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnUnlockItemSlotServerRpc", OnUnlockItemSlotServerRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.RequestSyncHeldObjectsServerRpc", RequestSyncHeldObjectsServerRpc);
            }
            else
            {
                isSynced = false;
                //unlockableReservedItemSlotsDict = canUseModDisabledOnHost ? new Dictionary<string, ReservedItemSlotData>(ReservedItemSlotData.allReservedItemSlotData) : new Dictionary<string, ReservedItemSlotData>();
                unlockableReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();
                unlockableReservedItemSlots = unlockableReservedItemSlotsDict.Values.ToList();

                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnSwapHotbarClientRpc", OnSwapHotbarClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.RequestSyncClientRpc", RequestSyncClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnUnlockItemSlotClientRpc", OnUnlockItemSlotClientRpc);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.RequestSyncHeldObjectsClientRpc", RequestSyncHeldObjectsClientRpc);

                RequestSyncFromServer();
            }

            UpdateReservedItemsList();
        }


        internal static void AddReservedItemSlotData(ReservedItemSlotData itemSlotData)
        {
            if (itemSlotData == null)
                return;

            if (unlockableReservedItemSlotsDict != null && !unlockableReservedItemSlotsDict.ContainsKey(itemSlotData.slotName))
            {
                unlockableReservedItemSlotsDict.Add(itemSlotData.slotName, itemSlotData);
                unlockableReservedItemSlots.Add(itemSlotData);
            }
        }


        internal static void UpdateReservedItemsList()
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


        private static void RequestSyncFromServer()
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            Plugin.Log("Requesting sync with server.");
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.RequestSyncServerRpc", NetworkManager.ServerClientId, new FastBufferWriter(0, Allocator.Temp));
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


        private static void SendSync(ulong clientId = 0, bool sendToAllClients = false)
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
            writer.WriteValue(enablePurchasingItemSlots); // bool
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
                    Plugin.Log("Sending slot to client: " + itemSlotData.slotName + " Unlocked: " + itemSlotData.isUnlocked);
                    writer.WriteValue(itemSlotData.isUnlocked); // bool
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

            hostHasMod = true;
            unlockableReservedItemSlots = new List<ReservedItemSlotData>();
            unlockableReservedItemSlotsDict = new Dictionary<string, ReservedItemSlotData>();

            Plugin.Log("Receiving sync from server.");
            if (SessionManager.unlockedReservedItemSlots != null && SessionManager.unlockedReservedItemSlots.Count > 0)
                SessionManager.ResetProgress(true);

            reader.ReadValue(out bool enablePurchasingItemSlots);
            reader.ReadValue(out int numEntries);

            SyncManager.enablePurchasingItemSlots = enablePurchasingItemSlots;

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
            OnSyncedWithServer();

            Plugin.Log("Received sync for " + unlockableReservedItemSlotsDict.Count + " reserved item slots.");
        }


        private static void OnSyncedWithServer()
        {
            isSynced = true;
            if (hostHasMod)
            {
                foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                {
                    if (IsReservedItem(item.itemName))
                        item.canBeGrabbedBeforeGameStart = true;
                }
            }
            localPlayerController.StartCoroutine(OnSyncedWithServerDelayed());
        }


        private static IEnumerator OnSyncedWithServerDelayed()
        {
            yield return new WaitForSeconds(3f);

            foreach (var playerData in ReservedPlayerData.allPlayerData.Values)
            {
                playerData.hotbarSize = playerData.itemSlots.Length;
                playerData.reservedHotbarStartIndex = playerData.hotbarSize;
            }

            if (!NetworkManager.Singleton.IsServer && hostHasMod)
                RequestSyncHeldObjects();
            SessionManager.UnlockAllPendingItemSlots();
        }




        private static void RequestSyncHeldObjects()
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer || requestedSyncHeldObjects)
                return;

            Plugin.Log("Requesting sync held objects from server.");
            requestedSyncHeldObjects = true;
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.RequestSyncHeldObjectsServerRpc", NetworkManager.ServerClientId, new FastBufferWriter(0, Allocator.Temp));
        }


        // ServerRpc
        private static void RequestSyncHeldObjectsServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            var clientIds = new List<ushort>();
            var selectedItemSlot = new List<short>();
            var inventoryIndexes = new Dictionary<ushort, List<short>>();
            var reservedItemNetworkIds = new Dictionary<ushort, List<ulong>>();

            int syncBufferSize = sizeof(short);

            foreach (var playerData in ReservedPlayerData.allPlayerData.Values)
            {
                ushort syncClientId = (ushort)playerData.playerController.actualClientId;
                ushort syncPlayerId = (ushort)playerData.playerController.playerClientId;
                if (syncClientId == clientId)
                    continue;

                if (syncClientId == 0 && !playerData.isLocalPlayer)
                    continue;

                for (short inventoryIndex = (short)playerData.reservedHotbarStartIndex; inventoryIndex < (short)playerData.reservedHotbarEndIndexExcluded; inventoryIndex++)
                {
                    short reservedItemSlotIndex = (short)(inventoryIndex - (short)playerData.reservedHotbarStartIndex);
                    var item = playerData.itemSlots[inventoryIndex];
                    if (item != null)
                    {
                        if (!inventoryIndexes.ContainsKey(syncPlayerId))
                        {
                            inventoryIndexes.Add(syncPlayerId, new List<short>());
                            reservedItemNetworkIds.Add(syncPlayerId, new List<ulong>());
                        }
                        inventoryIndexes[syncPlayerId].Add(reservedItemSlotIndex);
                        reservedItemNetworkIds[syncPlayerId].Add(item.NetworkObjectId);
                    }
                }

                if (inventoryIndexes.ContainsKey(syncPlayerId) && inventoryIndexes[syncPlayerId].Count > 0)
                {
                    syncBufferSize += sizeof(ushort); // syncClientId
                    syncBufferSize += sizeof(short); // selected item slot
                    syncBufferSize += sizeof(short); // num reserved items
                    syncBufferSize += (sizeof(short) + sizeof(ulong)) * inventoryIndexes[syncPlayerId].Count;
                    clientIds.Add(syncPlayerId);
                    selectedItemSlot.Add((short)playerData.currentItemSlot);
                }
            }

            Plugin.Log("Receiving sync held objects request from client with id: " + clientId + ". " + clientIds.Count + " players are currently holding a reserved item.");

            var writer = new FastBufferWriter(syncBufferSize, Allocator.Temp);
            writer.WriteValue((short)clientIds.Count);

            for (int i = 0; i < clientIds.Count; i++)
            {
                ushort syncClientId = clientIds[i];
                short selectedReservedItemSlot = selectedItemSlot[i];
                short numElements = (short)inventoryIndexes[syncClientId].Count;

                writer.WriteValue(syncClientId);
                writer.WriteValue(selectedReservedItemSlot);
                writer.WriteValue(numElements);

                for (int j = 0; j < numElements; j++)
                {
                    short inventoryIndex = inventoryIndexes[syncClientId][j];
                    ulong networkObjectId = reservedItemNetworkIds[syncClientId][j];
                    
                    writer.WriteValue(inventoryIndex);
                    writer.WriteValue(networkObjectId);
                }
            }
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.RequestSyncHeldObjectsClientRpc", clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }


        // ClientRpc
        private static void RequestSyncHeldObjectsClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            reader.ReadValue(out short numPlayersToSyncItems);
            Plugin.Log("Receiving sync held objects from server. Number of players already holding a reserved item: " + numPlayersToSyncItems);

            for (int i = 0; i < numPlayersToSyncItems; i++)
            {
                reader.ReadValue(out ushort syncPlayerId);
                reader.ReadValue(out short currentlySelectedItemSlot);
                reader.ReadValue(out short numHeldReservedItems);

                var playerController = GetPlayerControllerByPlayerId(syncPlayerId);
                ReservedPlayerData playerData = null;
                if (playerController != null)
                    ReservedPlayerData.allPlayerData.TryGetValue(playerController, out playerData);

                for (int j = 0; j < numHeldReservedItems; j++)
                {
                    reader.ReadValue(out short reservedItemSlotIndex);
                    reader.ReadValue(out ulong networkObjectId);
                    var grabbableObject = GetGrabbableObjectByNetworkId(networkObjectId);

                    if (grabbableObject != null && playerData != null && reservedItemSlotIndex >= 0 && reservedItemSlotIndex < SessionManager.numReservedItemSlotsUnlocked)
                    {
                        int indexInInventory = reservedItemSlotIndex + playerData.reservedHotbarStartIndex;
                        grabbableObject.isHeld = true;
                        playerData.itemSlots[indexInInventory] = grabbableObject;
                        grabbableObject.parentObject = playerController.serverItemHolder;
                        grabbableObject.playerHeldBy = playerController;
                        bool currentlySelected = currentlySelectedItemSlot == indexInInventory;

                        grabbableObject.EnablePhysics(false);

                        if (currentlySelected)
                        {
                            grabbableObject.EquipItem();
                            playerController.currentlyHeldObjectServer = grabbableObject;
                            playerController.isHoldingObject = true;
                            playerController.twoHanded = grabbableObject.itemProperties.twoHanded;
                            playerController.twoHandedAnimation = grabbableObject.itemProperties.twoHandedAnimation;
                            playerController.currentItemSlot = indexInInventory;
                        }
                        else
                            grabbableObject.PocketItem();
                    }
                }
            }
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




        private static void SendSwapHotbarUpdateToServer(int hotbarSlot)
        {
            if (!NetworkManager.Singleton.IsClient)
                return;

            if (NetworkManager.Singleton.IsServer)
            {
                SendSwapHotbarUpdateToClients(localPlayerController.actualClientId, hotbarSlot);
                return;
            }

            var writer = new FastBufferWriter(sizeof(short), Allocator.Temp);
            writer.WriteValue((short)hotbarSlot);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.OnSwapHotbarServerRpc", NetworkManager.ServerClientId, writer);
        }


        // ServerRpc
        private static void OnSwapHotbarServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            reader.ReadValue(out short hotbarIndex);
            if (NetworkManager.Singleton.IsClient && clientId != localPlayerController.actualClientId)
            {
                Plugin.Log("Receiving OnSwapReservedHotbar update from client. ClientId: " + clientId + " Slot: " + hotbarIndex);
                TryUpdateClientHotbarSlot(clientId, hotbarIndex);
            }
            SendSwapHotbarUpdateToClients(clientId, hotbarIndex);
        }


        internal static void SendSwapHotbarUpdateToClients(ulong clientId, int hotbarIndex)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            var writer = new FastBufferWriter(sizeof(short) + sizeof(uint), Allocator.Temp);
            writer.WriteValueSafe((short)hotbarIndex);
            writer.WriteValueSafe((uint)clientId);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("ReservedItemSlotCore.OnSwapHotbarClientRpc", writer);
        }


        // ClientRpc
        private static void OnSwapHotbarClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
                return;

            reader.ReadValue(out short hotbarIndex);
            reader.ReadValue(out uint swappingClientId);

            if (swappingClientId == localPlayerController.actualClientId)
                return;

            Plugin.Log("Receiving OnSwapReservedHotbar update from client. ClientId: " + swappingClientId + " Slot: " + hotbarIndex);
            if (TryUpdateClientHotbarSlot(swappingClientId, hotbarIndex))
                return;

            Plugin.Log("Failed to receive hotbar swap index from Client: " + swappingClientId);
        }


        private static bool TryUpdateClientHotbarSlot(ulong clientId, int hotbarSlot)
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


        /// <summary>
        /// Swaps hotbar slots on your local player, and sends this to the server as well.
        /// </summary>
        /// <param name="hotbarIndex"></param>
        public static void SwapHotbarSlot(int hotbarIndex)
        {
            SendSwapHotbarUpdateToServer(hotbarIndex);
            CallSwitchToItemSlotMethod(localPlayerController, hotbarIndex);
        }


        internal static void CallSwitchToItemSlotMethod(PlayerControllerB playerController, int hotbarIndex)
        {
            if (playerController == null || playerController.ItemSlots == null || hotbarIndex < 0 || hotbarIndex >= playerController.ItemSlots.Length)
                return;
            if (playerController == localPlayerController)
            {
                ShipBuildModeManager.Instance.CancelBuildMode(true);
                playerController.playerBodyAnimator.SetBool("GrabValidated", value: true);
            }

            PlayerPatcher.SwitchToItemSlot(playerController, hotbarIndex);

            if (playerController.currentlyHeldObjectServer != null)
                playerController.currentlyHeldObjectServer.gameObject.GetComponent<AudioSource>().PlayOneShot(playerController.currentlyHeldObjectServer.itemProperties.grabSFX, 0.6f);
        }


        internal static PlayerControllerB GetPlayerControllerByClientId(ulong clientId)
        {
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                var playerController = StartOfRound.Instance.allPlayerScripts[i];
                if (playerController.actualClientId == clientId)
                    return playerController;
            }
            return null;
        }


        internal static PlayerControllerB GetPlayerControllerByPlayerId(ulong playerId)
        {
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                var playerController = StartOfRound.Instance.allPlayerScripts[i];
                if (playerController.playerClientId == playerId)
                    return playerController;
            }
            return null;
        }


        internal static GrabbableObject GetGrabbableObjectByNetworkId(ulong networkObjectId)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject))
                return networkObject.GetComponentInChildren<GrabbableObject>();
            return null;
        }
    }
}