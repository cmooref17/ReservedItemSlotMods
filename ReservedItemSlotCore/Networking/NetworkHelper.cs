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
        static int NONE_EXEC_STAGE = 0;
        static int SERVER_EXEC_STAGE = 1;
        static int CLIENT_EXEC_STAGE = 2;

        public static int GetExecStage(NetworkBehaviour __instance) => (int)Traverse.Create(__instance).Field("__rpc_exec_stage").GetValue();

        public static bool IsClientExecStage(NetworkBehaviour __instance) => GetExecStage(__instance) == CLIENT_EXEC_STAGE;
        public static bool IsServerExecStage(NetworkBehaviour __instance) => GetExecStage(__instance) == SERVER_EXEC_STAGE;

        public static bool IsValidClientRpcExecStage(NetworkBehaviour __instance)
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
