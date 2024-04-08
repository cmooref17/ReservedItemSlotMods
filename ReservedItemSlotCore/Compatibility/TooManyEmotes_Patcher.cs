using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes;
using TooManyEmotes.Networking;


namespace ReservedItemSlotCore.Compatibility
{
    internal static class TooManyEmotes_Patcher
    {
        public static bool Enabled { get { return Plugin.IsModLoaded("FlipMods.TooManyEmotes"); } }

        public static bool IsLocalPlayerPerformingCustomEmote()
        {
            if (EmoteControllerPlayer.emoteControllerLocal != null && EmoteControllerPlayer.emoteControllerLocal.IsPerformingCustomEmote())
                return true;
            return false;
        }

        public static bool CanMoveWhileEmoting()
        {
            if (ConfigSync.instance != null)
                return ConfigSync.instance.syncEnableMovingWhileEmoting;
            return false;
        }
    }
}