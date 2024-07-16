using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReservedItemSlotCore.Compatibility
{
    internal static class AdvancedCompany_Patcher
    {
        public static bool Enabled { get { return Plugin.IsModLoaded("com.potatoepet.AdvancedCompany"); } }
    }
}
