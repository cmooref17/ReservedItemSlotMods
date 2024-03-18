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
        [HarmonyPostfix]
        private static void RecordOriginalItemNames(StartOfRound __instance)
        {
            foreach (var item in __instance.allItemsList.itemsList)
            {
                string originalItemName = item.itemName;

                if (item != null && originalItemName.Length > 0)
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

            return GetItemName(grabbableObject.itemProperties);
        }


        internal static string GetItemName(Item item)
        {
            if (item == null)
                return "";

            if (itemToNameMap.TryGetValue(item, out var itemName))
                return itemName;

            return "";
        }
    }
}
