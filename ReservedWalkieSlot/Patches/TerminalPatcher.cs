using GameNetcodeStuff;
using HarmonyLib;
using ReservedWalkieSlot.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
namespace ReservedWalkieSlot.Patches
{
    [HarmonyPatch(typeof(Terminal))]
    public class TerminalPatcher
    {
        static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }
        static bool activatingWalkie;

        public static void Prefix(Terminal __instance)
        {
            if (localPlayerController == null)
                return;

            activatingWalkie = localPlayerController.activatingItem;

            var walkie = WalkiePatcher.GetMainWalkie(localPlayerController);
            if (walkie == null || walkie.playerHeldBy == null || walkie.playerHeldBy != localPlayerController)
                return;

            if (localPlayerController.activatingItem && walkie.speakingIntoWalkieTalkie)
                localPlayerController.activatingItem = false;
        }


        public static void Postfix(ShipBuildModeManager __instance)
        {
            if (localPlayerController == null)
                return;

            localPlayerController.activatingItem = activatingWalkie;
        }
    }
}
*/