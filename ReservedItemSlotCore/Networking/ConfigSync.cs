using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using ReservedItemSlotCore.Config;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/*
namespace ReservedItemSlotCore.Networking
{
    [Serializable]
    [HarmonyPatch]
    public class ConfigSync
    {
        public static bool isSynced = false;
        public static ConfigSync defaultConfig;
        public static ConfigSync instance;
        public static HashSet<ulong> syncedClients;

        public bool disablePurchasingReservedSlots = true;

        
        public ConfigSync()
        {
            disablePurchasingReservedSlots = ConfigSettings.disablePurchasingReservedSlots.Value;
        }


        public static void BuildDefaultConfigSync()
        {
            defaultConfig = new ConfigSync();
            instance = new ConfigSync();
        }



        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        public static void ResetValues()
        {
            isSynced = false;
        }


        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        public static void Init(PlayerControllerB __instance)
        {
            if (isSynced)
                return;
            isSynced = NetworkManager.Singleton.IsServer;
            SyncManager.isSynced = false;
            //SyncManager.requestedSync = false;
            if (NetworkManager.Singleton.IsServer)
            {
                syncedClients = new HashSet<ulong>();
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnRequestConfigSyncServerRpc", OnRequestConfigSyncServerRpc);
            }
            else
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ReservedItemSlotCore.OnRequestConfigSyncClientRpc", OnRequestConfigSyncClientRpc);
                RequestConfigSync();
            }
        }


        public static void RequestConfigSync()
        {
            if (!NetworkManager.Singleton.IsClient)
                return;
            Plugin.Log("Requesting config sync from server");
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.OnRequestConfigSyncServerRpc", NetworkManager.ServerClientId, new FastBufferWriter(0, Allocator.Temp));
        }


        static void OnRequestConfigSyncServerRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;
            Plugin.Log("Receiving config sync request from client: " + clientId);
            syncedClients.Add(clientId);
            //SyncManager.syncedClients.Remove(clientId);
            var writer = new FastBufferWriter(sizeof(bool), Allocator.Temp);
            writer.WriteValue(instance.disablePurchasingReservedSlots);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("ReservedItemSlotCore.OnRequestConfigSyncClientRpc", clientId, writer);
        }


        private static void OnRequestConfigSyncClientRpc(ulong clientId, FastBufferReader reader)
        {
            if (!NetworkManager.Singleton.IsClient)
                return;

            Plugin.Log("Receiving config sync from server.");
            reader.ReadValue(out instance.disablePurchasingReservedSlots);
            isSynced = true;
        }
    }
}
*/
