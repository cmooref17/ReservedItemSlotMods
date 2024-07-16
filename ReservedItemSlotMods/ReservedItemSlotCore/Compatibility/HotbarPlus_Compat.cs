using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TooManyEmotes;
using TooManyEmotes.Networking;


namespace ReservedItemSlotCore.Compatibility
{
    internal static class HotbarPlus_Compat
    {
        public static bool Enabled { get { return Plugin.IsModLoaded("FlipMods.HotbarPlus"); } }
    }
}