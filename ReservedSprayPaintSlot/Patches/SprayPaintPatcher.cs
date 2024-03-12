using HarmonyLib;
using UnityEngine;
using GameNetcodeStuff;
using ReservedItemSlotCore.Patches;
using ReservedItemSlotCore.Networking;
using ReservedSprayPaintSlot.Config;
using System.Diagnostics.Eventing.Reader;
using ReservedItemSlotCore;
using ReservedItemSlotCore.Data;

namespace ReservedSprayPaintSlot.Patches
{
	[HarmonyPatch]
	static internal class SprayPaintPatcher
    {
        public static PlayerControllerB localPlayerController { get { return StartOfRound.Instance?.localPlayerController; } }

        public static PlayerControllerB GetPreviousPlayerHeldBy(SprayPaintItem sprayPaintItem) => (PlayerControllerB)Traverse.Create(sprayPaintItem).Field("previousPlayerHeldBy").GetValue();

        public static SprayPaintItem GetMainSprayPaint(PlayerControllerB playerController) => GetCurrentlySelectedSprayPaint(playerController) ?? GetReservedSprayPaint(playerController);
        public static SprayPaintItem GetReservedSprayPaint(PlayerControllerB playerController) => SessionManager.unlockedReservedItemSlots.Contains(Plugin.sprayPaintSlotData) && ReservedPlayerData.allPlayerData.TryGetValue(playerController, out var playerData) ? playerData.GetReservedItem(Plugin.sprayPaintSlotData) as SprayPaintItem: null;
        public static SprayPaintItem GetCurrentlySelectedSprayPaint(PlayerControllerB playerController) => playerController.currentItemSlot >= 0 && playerController.currentItemSlot < playerController.ItemSlots.Length ? playerController?.ItemSlots[playerController.currentItemSlot] as SprayPaintItem : null;
    }
}