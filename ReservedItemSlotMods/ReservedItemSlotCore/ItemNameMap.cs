using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReservedItemSlotCore
{
    [HarmonyPatch]
    internal static class ItemNameMap
    {
        private static Dictionary<string, Item> originalNameToItemMap = new Dictionary<string, Item>();
        private static Dictionary<Item, string> itemToNameMap = new Dictionary<Item, string>();


        [HarmonyPatch(typeof(StartOfRound), "Start")]
        [HarmonyPrefix]
        private static void RecordOriginalItemNames(StartOfRound __instance)
        {
            var itemsList = __instance?.allItemsList?.itemsList;
            if (itemsList == null)
            {
                Plugin.LogError("Failed to record original item names. This might be fine if you're not using translation/localization mods. (no guarantees)");
                return;
            }
            foreach (var item in itemsList)
            {
                string originalItemName = item?.itemName;

                if (!string.IsNullOrEmpty(originalItemName))
                {
                    if (!itemToNameMap.ContainsKey(item))
                        itemToNameMap.Add(item, originalItemName);
                    if (!originalNameToItemMap.ContainsKey(originalItemName))
                        originalNameToItemMap.Add(originalItemName, item);
                }
            }
        }


        internal static string GetItemName(GrabbableObject grabbableObject)
        {
            if (grabbableObject?.itemProperties == null)
                return "";

            string itemName = GetItemName(grabbableObject.itemProperties);
            return itemName != null ? itemName : "";
        }


        internal static string GetItemName(Item item)
        {
            if (item == null)
                return "";

            if (itemToNameMap.TryGetValue(item, out var itemName) && itemName != null)
                return itemName;

            return "";
        }
    }
}
