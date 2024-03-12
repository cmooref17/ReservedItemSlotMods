using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ReservedSprayPaintSlot.Config;


namespace ReservedSprayPaintSlot.Patches
{
    [HarmonyPatch]
    public class SprayPaintTweaks
    {
        public static float GetSprayCanTankValue(SprayPaintItem sprayPaintItem) => (float)Traverse.Create(sprayPaintItem).Field("sprayCanTank").GetValue();
        public static void SetSprayCanTankValue(SprayPaintItem sprayPaintItem, float value) => Traverse.Create(sprayPaintItem).Field("sprayCanTank").SetValue(value);


        [HarmonyPatch(typeof(SprayPaintItem), "Start")]
        [HarmonyPrefix]
        public static void InitSprayPaint(SprayPaintItem __instance)
        {
            SetSprayCanTankValue(__instance, ConfigSettings.sprayPaintCapacityMultiplier.Value);
        }


        [HarmonyPatch(typeof(SprayPaintItem), "LoadItemSaveData")]
        [HarmonyPostfix]
        public static void OnLoadValues(int saveData, SprayPaintItem __instance)
        {
            float capacity = Mathf.Clamp(GetSprayCanTankValue(__instance) * ConfigSettings.sprayPaintCapacityMultiplier.Value, 0, ConfigSettings.sprayPaintCapacityMultiplier.Value);
            SetSprayCanTankValue(__instance, capacity);
            Plugin.Log("Loading spraypaint save data. Remaining capacity: " + capacity);
        }


        [HarmonyPatch(typeof(SprayPaintItem), "GetItemDataToSave")]
        [HarmonyPostfix]
        public static void OnSaveValues(ref int __result, SprayPaintItem __instance)
        {
            __result = (int)Mathf.Clamp(__result / ConfigSettings.sprayPaintCapacityMultiplier.Value, 0, 100);
        }

    }
}
