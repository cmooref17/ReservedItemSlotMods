using HarmonyLib;
using UnityEngine;
using GameNetcodeStuff;
using ReservedItemSlotCore;
using ReservedItemSlotCore.Patches;
using ReservedItemSlotCore.Data;
using ReservedWalkieSlot.Config;
using ReservedItemSlotCore.Networking;


namespace ReservedWalkieSlot.Patches
{
    [HarmonyPatch]
    public static class WalkiePatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static WalkieTalkie GetMainWalkie(PlayerControllerB playerController) => GetCurrentlySelectedWalkie(playerController) ?? GetReservedWalkie(playerController);
        public static WalkieTalkie GetReservedWalkie(PlayerControllerB playerController) => SyncManager.unlockableReservedItemSlotsDict.TryGetValue(Plugin.walkieSlotData.slotName, out var reservedItemSlot) && reservedItemSlot.slotUnlocked && ReservedPlayerData.allPlayerData.TryGetValue(playerController, out var playerData) ? playerData.GetReservedItem(reservedItemSlot) as WalkieTalkie : null;
        public static WalkieTalkie GetCurrentlySelectedWalkie(PlayerControllerB playerController) => playerController.currentItemSlot >= 0 && playerController.currentItemSlot < playerController.ItemSlots.Length ? playerController?.ItemSlots[playerController.currentItemSlot] as WalkieTalkie : null;
    }
}