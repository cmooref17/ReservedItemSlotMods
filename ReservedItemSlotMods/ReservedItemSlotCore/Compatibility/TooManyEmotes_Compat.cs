using TooManyEmotes;
using TooManyEmotes.Patches;

namespace ReservedItemSlotCore.Compatibility
{
    internal static class TooManyEmotes_Compat
    {
        public static bool Enabled { get { return Plugin.IsModLoaded("FlipMods.TooManyEmotes"); } }

        public static bool IsLocalPlayerPerformingCustomEmote()
        {
            if (EmoteControllerPlayer.emoteControllerLocal != null && EmoteControllerPlayer.emoteControllerLocal.IsPerformingCustomEmote())
                return true;
            return false;
        }

        public static bool CanMoveWhileEmoting() => false; // ThirdPersonEmoteController.allowMovingWhileEmoting;
    }
}