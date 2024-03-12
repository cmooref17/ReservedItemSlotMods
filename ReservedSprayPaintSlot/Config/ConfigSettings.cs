using BepInEx.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;


namespace ReservedSprayPaintSlot.Config
{
    public static class ConfigSettings
    {
        public static ConfigEntry<bool> hideSprayPaintMeshWaist;
        public static ConfigEntry<float> sprayPaintCapacityMultiplier;
        public static ConfigEntry<int> overrideItemSlotPriority;
        public static ConfigEntry<int> overridePurchasePrice;
        public static ConfigEntry<string> additionalItemsInSlot;

        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();


        public static void BindConfigSettings()
        {
            Plugin.Log("BindingConfigs");

            hideSprayPaintMeshWaist = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "HideSprayPaintOnWaist", false, "Hides the spray paint mesh while on your waist."));
            sprayPaintCapacityMultiplier = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "SprayPaintCapacityMultiplier", 10f, "Extends the max capacity of spraypaint cans by this multiplier. This setting will soon be host only, and will sync with all non-host clients."));
            overrideItemSlotPriority = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "OverrideSprayPaintPriority", 30, "Manually set the priority for this item slot. Higher priority slots will come first in the reserved item slots, which will appear below the other slots. Negative priority items will appear on the left side of the screen, this is disabled in the core mod's config."));
            overridePurchasePrice = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "OverrideSprayPaintSlotPrice", 100, "Manually set the price for this item in the store. Setting 0 will force this item to be unlocked immediately after the game starts."));
            additionalItemsInSlot = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "AdditionalItemsInSlot", "", "Syntax: \"Item1,Item name2\" (without quotes). When adding items, use the item's name as it appears in game. Include spaces if there are spaces in the item name. Adding items that do not exist, or that are from a mod which is not enabled will not cause any problems. As of now, additional items added to reserved item slots cannot be seen on players while holstered."));

            additionalItemsInSlot.Value = additionalItemsInSlot.Value.Replace(", ", ",");
            sprayPaintCapacityMultiplier.Value = Mathf.Max(sprayPaintCapacityMultiplier.Value, 0);

            TryRemoveOldConfigSettings();
        }


        public static ConfigEntry<T> AddConfigEntry<T>(ConfigEntry<T> configEntry)
        {
            currentConfigEntries.Add(configEntry.Definition.Key, configEntry);
            return configEntry;
        }


        public static string[] ParseAdditionalItems()
        {
            if (additionalItemsInSlot.Value == "")
                return new string[0];

            List<string> additionalItemNames = new List<string>(additionalItemsInSlot.Value.Split(','));
            additionalItemNames = additionalItemNames.Where(s => s.Length >= 1).ToList();
            return additionalItemNames.ToArray();
        }


        public static void TryRemoveOldConfigSettings()
        {
            HashSet<string> headers = new HashSet<string>();
            HashSet<string> keys = new HashSet<string>();

            foreach (ConfigEntryBase entry in currentConfigEntries.Values)
            {
                headers.Add(entry.Definition.Section);
                keys.Add(entry.Definition.Key);
            }

            try
            {

                ConfigFile config = Plugin.instance.Config;
                string filepath = config.ConfigFilePath;

                if (File.Exists(filepath))
                {
                    string contents = File.ReadAllText(filepath);
                    string[] lines = File.ReadAllLines(filepath); // Because contents.Split('\n') is adding strange characters...

                    string currentHeader = "";

                    for (int i = 0; i < lines.Length; i++)
                    {
                        lines[i] = lines[i].Replace("\n", "");
                        if (lines[i].Length <= 0)
                            continue;

                        if (lines[i].StartsWith("["))
                        {
                            if (currentHeader != "" && !headers.Contains(currentHeader))
                            {
                                currentHeader = "[" + currentHeader + "]";
                                int index0 = contents.IndexOf(currentHeader);
                                int index1 = contents.IndexOf(lines[i]);
                                contents = contents.Remove(index0, index1 - index0);
                            }
                            currentHeader = lines[i].Replace("[", "").Replace("]", "").Trim();
                        }

                        else if (currentHeader != "")
                        {
                            if (i <= (lines.Length - 4) && lines[i].StartsWith("##"))
                            {
                                int numLinesEntry = 1;
                                while (i + numLinesEntry < lines.Length && lines[i + numLinesEntry].Length > 3) // 3 because idc
                                    numLinesEntry++;

                                if (headers.Contains(currentHeader))
                                {
                                    int indexAssignOperator = lines[i + numLinesEntry - 1].IndexOf("=");
                                    string key = lines[i + numLinesEntry - 1].Substring(0, indexAssignOperator - 1);
                                    if (!keys.Contains(key))
                                    {
                                        int index0 = contents.IndexOf(lines[i]);
                                        int index1 = contents.IndexOf(lines[i + numLinesEntry - 1]) + lines[i + numLinesEntry - 1].Length;
                                        contents = contents.Remove(index0, index1 - index0);
                                    }
                                }
                                i += (numLinesEntry - 1);
                            }
                            else if (lines[i].Length > 3)
                                contents = contents.Replace(lines[i], "");
                        }
                    }

                    if (!headers.Contains(currentHeader))
                    {
                        currentHeader = "[" + currentHeader + "]";
                        int index0 = contents.IndexOf(currentHeader);
                        contents = contents.Remove(index0, contents.Length - index0);
                    }

                    while (contents.Contains("\n\n\n"))
                        contents = contents.Replace("\n\n\n", "\n\n");

                    File.WriteAllText(filepath, contents);
                    config.Reload();
                }
            }
            catch { } // Probably okay
        }
    }
}
