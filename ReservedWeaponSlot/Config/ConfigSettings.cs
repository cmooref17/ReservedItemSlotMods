using BepInEx.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace ReservedWeaponSlot.Config
{
    public static class ConfigSettings
    {
        public static ConfigEntry<int> overrideMeleeSlotPriority;
        public static ConfigEntry<int> overrideMeleeSlotPrice;

        public static ConfigEntry<int> overrideRangedSlotPriority;
        public static ConfigEntry<int> overrideRangedSlotPrice;

        public static ConfigEntry<bool> combineMeleeAndRangedWeaponSlots;
        public static ConfigEntry<int> overrideCombinedWeaponSlotPrice;

        public static ConfigEntry<bool> disableReservedAmmoSlot;
        public static ConfigEntry<int> overrideAmmoSlotPriority;
        public static ConfigEntry<int> overrideAmmoSlotPrice;

        public static ConfigEntry<string> additionalMeleeWeaponsInSlot;
        public static ConfigEntry<string> additionalRangedWeaponsInSlot;
        public static ConfigEntry<string> additionalAmmoInSlot;

        public static ConfigEntry<string> removeItemsFromMeleeWeaponsSlot;
        public static ConfigEntry<string> removeItemsFromRangedWeaponsSlot;
        public static ConfigEntry<string> removeItemsFromAmmoSlot;

        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();


        public static void BindConfigSettings()
        {
            Plugin.Log("BindingConfigs");

            
            overrideMeleeSlotPriority = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "OverrideMeleeWeaponSlotPriority", 60, "[Host only] Manually set the priority for this item slot. Higher priority slots will come first in the reserved item slots, which will appear below the other slots. Negative priority items will appear on the left side of the screen, this is disabled in the core mod's config."));
            overrideMeleeSlotPrice = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "OverrideMeleeWeaponSlotPrice", 150, "[Host only] Manually set the price for this item in the store. Setting 0 will force this item to be unlocked immediately after the game starts."));

            overrideRangedSlotPriority = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "OverrideRangedWeaponSlotPriority", 59, "[Host only] Manually set the priority for this item slot. Higher priority slots will come first in the reserved item slots, which will appear below the other slots. Negative priority items will appear on the left side of the screen, this is disabled in the core mod's config."));
            overrideRangedSlotPrice = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "OverrideRangedWeaponSlotPrice", 250, "[Host only] Manually set the price for this item in the store. Setting 0 will force this item to be unlocked immediately after the game starts."));

            combineMeleeAndRangedWeaponSlots = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "CombineMeleeAndRangedWeaponSlots", true, "[Host only]     "));
            overrideCombinedWeaponSlotPrice = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "OverrideCombinedWeaponSlotPrice", 400, "[Host only] Only applies if CombineMeleeAndRangedWeaponSlots is true. Manually set the price for this item in the store. Setting 0 will force this item to be unlocked immediately after the game starts."));

            disableReservedAmmoSlot = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "DisableReservedAmmoSlot", false, "[Host only] Disables the reserved ammo slot. Will sync with clients."));
            overrideAmmoSlotPriority = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "OverrideAmmoSlotPriority", -10, "[Host only] Manually set the priority for this item slot. Higher priority slots will come first in the reserved item slots, which will appear below the other slots. Negative priority items will appear on the left side of the screen, this is disabled in the core mod's config."));
            overrideAmmoSlotPrice = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "OverrideAmmoSlotPrice", 150, "[Host only] Manually set the price for this item in the store. Setting 0 will force this item to be unlocked immediately after the game starts."));

            additionalMeleeWeaponsInSlot = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "AdditionalMeleeWeaponsInSlot", "", "[Host only] Syntax: \"Item1,Item name2\" (without quotes). When adding items, use the item's name as it appears in game. Include spaces if there are spaces in the item name. Adding items that do not exist, or that are from a mod which is not enabled will not cause any problems. As of now, additional items added to reserved item slots cannot be seen on players while holstered."));
            additionalRangedWeaponsInSlot = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "AdditionalRangedWeaponsInSlot", "", "[Host only] Syntax: \"Item1,Item name2\" (without quotes). When adding items, use the item's name as it appears in game. Include spaces if there are spaces in the item name. Adding items that do not exist, or that are from a mod which is not enabled will not cause any problems. As of now, additional items added to reserved item slots cannot be seen on players while holstered."));
            additionalAmmoInSlot = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "AdditionalAmmoInSlot", "", "[Host only] Syntax: \"Item1,Item name2\" (without quotes). When adding items, use the item's name as it appears in game. Include spaces if there are spaces in the item name. Adding items that do not exist, or that are from a mod which is not enabled will not cause any problems. As of now, additional items added to reserved item slots cannot be seen on players while holstered."));

            removeItemsFromMeleeWeaponsSlot = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "RemoveMeleeWeaponsFromSlot", "", "[Host only] Syntax: \"Item1,Item name2\" (without quotes). Removes the specified items from this reserved item slot. When removing items, use the item's name as it appears in game. Include spaces if there are spaces in the item name. Adding items that do not exist, or that are from a mod which is not enabled will not cause any problems. As of now, additional items added to reserved item slots cannot be seen on players while holstered."));
            removeItemsFromRangedWeaponsSlot = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "RemoveRangedWeaponsFromSlot", "", "[Host only] Syntax: \"Item1,Item name2\" (without quotes). Removes the specified items from this reserved item slot. When removing items, use the item's name as it appears in game. Include spaces if there are spaces in the item name. Adding items that do not exist, or that are from a mod which is not enabled will not cause any problems. As of now, additional items added to reserved item slots cannot be seen on players while holstered."));
            removeItemsFromAmmoSlot = AddConfigEntry(Plugin.instance.Config.Bind("Server-side", "RemoveAmmoItemsFromSlot", "", "[Host only] Syntax: \"Item1,Item name2\" (without quotes). Removes the specified items from this reserved item slot.When removing items, use the item's name as it appears in game. Include spaces if there are spaces in the item name. Adding items that do not exist, or that are from a mod which is not enabled will not cause any problems. As of now, additional items added to reserved item slots cannot be seen on players while holstered."));

            additionalMeleeWeaponsInSlot.Value = additionalMeleeWeaponsInSlot.Value.Replace(", ", ",");
            additionalRangedWeaponsInSlot.Value = additionalRangedWeaponsInSlot.Value.Replace(", ", ",");
            additionalAmmoInSlot.Value = additionalAmmoInSlot.Value.Replace(", ", ",");
            removeItemsFromMeleeWeaponsSlot.Value = removeItemsFromMeleeWeaponsSlot.Value.Replace(", ", ",");
            removeItemsFromRangedWeaponsSlot.Value = removeItemsFromRangedWeaponsSlot.Value.Replace(", ", ",");
            removeItemsFromAmmoSlot.Value = removeItemsFromAmmoSlot.Value.Replace(", ", ",");

            TryRemoveOldConfigSettings();
        }


        public static ConfigEntry<T> AddConfigEntry<T>(ConfigEntry<T> configEntry)
        {
            currentConfigEntries.Add(configEntry.Definition.Key, configEntry);
            return configEntry;
        }


        public static string[] ParseAdditionalMeleeWeaponItems()
        {
            if (additionalMeleeWeaponsInSlot.Value == "")
                return new string[0];

            List<string> additionalItemNames = new List<string>(additionalMeleeWeaponsInSlot.Value.Split(','));
            additionalItemNames = additionalItemNames.Where(s => s.Length >= 1).ToList();
            return additionalItemNames.ToArray();
        }


        public static string[] ParseAdditionalRangedWeaponItems()
        {
            if (additionalRangedWeaponsInSlot.Value == "")
                return new string[0];

            List<string> additionalItemNames = new List<string>(additionalRangedWeaponsInSlot.Value.Split(','));
            additionalItemNames = additionalItemNames.Where(s => s.Length >= 1).ToList();
            return additionalItemNames.ToArray();
        }


        public static string[] ParseAdditionalAmmoItems()
        {
            if (additionalAmmoInSlot.Value == "")
                return new string[0];

            List<string> additionalItemNames = new List<string>(additionalAmmoInSlot.Value.Split(','));
            additionalItemNames = additionalItemNames.Where(s => s.Length >= 1).ToList();
            return additionalItemNames.ToArray();
        }


        public static string[] ParseRemoveMeleeWeaponItems()
        {
            if (removeItemsFromMeleeWeaponsSlot.Value == "")
                return new string[0];

            List<string> removeItemNames = new List<string>(removeItemsFromMeleeWeaponsSlot.Value.Split(','));
            removeItemNames = removeItemNames.Where(s => s.Length >= 1).ToList();
            return removeItemNames.ToArray();
        }


        public static string[] ParseRemoveRangedWeaponItems()
        {
            if (removeItemsFromRangedWeaponsSlot.Value == "")
                return new string[0];

            List<string> removeItemNames = new List<string>(removeItemsFromRangedWeaponsSlot.Value.Split(','));
            removeItemNames = removeItemNames.Where(s => s.Length >= 1).ToList();
            return removeItemNames.ToArray();
        }


        public static string[] ParseRemoveAmmoItems()
        {
            if (removeItemsFromAmmoSlot.Value == "")
                return new string[0];

            List<string> removeItemNames = new List<string>(removeItemsFromAmmoSlot.Value.Split(','));
            removeItemNames = removeItemNames.Where(s => s.Length >= 1).ToList();
            return removeItemNames.ToArray();
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