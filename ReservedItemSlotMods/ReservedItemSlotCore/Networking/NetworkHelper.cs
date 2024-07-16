using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;

namespace ReservedItemSlotCore.Networking
{
    internal static class NetworkHelper
    {
        private static int NONE_EXEC_STAGE = 0;
        private static int SERVER_EXEC_STAGE = 1;
        private static int CLIENT_EXEC_STAGE = 2;

        // THIS CODE IS SPECIALIZED FOR A SPECIFIC USE CASE

        internal static int GetExecStage(NetworkBehaviour __instance) => (int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue();

        internal static bool IsClientExecStage(NetworkBehaviour __instance) => GetExecStage(__instance) == CLIENT_EXEC_STAGE;
        internal static bool IsServerExecStage(NetworkBehaviour __instance) => GetExecStage(__instance) == SERVER_EXEC_STAGE;

        internal static bool IsValidClientRpcExecStage(NetworkBehaviour __instance)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
                return false;

            int rpcExecStage = (int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue();
            if ((networkManager.IsServer || networkManager.IsHost) && rpcExecStage != 2) // 2 = Client
                return false;

            return true;
        }
    }
}
