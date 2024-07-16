using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
//using GeneralImprovements.Items;
using System.Reflection;

namespace ReservedItemSlotCore.Compatibility
{
    internal static class GeneralImprovements_Patcher
    {
        public static bool Enabled { get { return Plugin.IsModLoaded("ShaosilGaming.GeneralImprovements"); } }

        public static void AddLightningIndicatorToItemSlotFrame(Image itemSlotFrame)
        {
            // TODO
        }
    }
}
