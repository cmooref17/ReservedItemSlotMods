using BepInEx.Configuration;
using ReservedItemSlotCore.Networking;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;


namespace ReservedItemSlotCore.Config
{
    public static class ConfigSettings
    {
        public static ConfigEntry<bool> enablePurchasingItemSlots;
        public static ConfigEntry<float> globalItemSlotPriceModifier;
        public static ConfigEntry<bool> forceEnableThisModIfNotEnabledOnHost;
        public static ConfigEntry<bool> displayNegativePrioritySlotsLeftSideOfScreen;
        public static ConfigEntry<bool> hideEmptyReservedItemSlots;
        //public static ConfigEntry<string> focusReservedHotbarHotkey;
        //public static ConfigEntry<bool> toggleFocusReservedHotbar;
        //public static ConfigEntry<bool> allowScrollingBetweenHotbars;
        public static ConfigEntry<bool> preventReservedItemSlotFade;
        public static ConfigEntry<bool> hideFocusHotbarTooltip;

        public static ConfigEntry<bool> showReservedItemsHolstered;
        public static ConfigEntry<bool> showReservedItemsHolsteredMaskedEnemy;

        public static ConfigEntry<bool> applyHotbarPlusItemSlotSize;
        public static ConfigEntry<bool> disableHotbarPlusEnergyBars;

        public static ConfigEntry<bool> verboseLogs;

        public static ConfigEntry<int> numCustomItemSlots;
        public static List<CustomItemSlotConfigEntry> customItemSlotConfigs = new List<CustomItemSlotConfigEntry>();

        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();


        public static void BindConfigSettings()
        {
            Plugin.Log("BindingConfigs");

            enablePurchasingItemSlots = AddConfigEntry(Plugin.instance.Config.Bind("Server", "EnablePurchasingItemSlots", false, "[Host only] Set to true to enable purchasing reserved item slots. If set to false, all players will start the game with all available reserved item slots."));
            globalItemSlotPriceModifier = AddConfigEntry(Plugin.instance.Config.Bind("Server", "GlobalItemSlotPriceModifier", 1f, "[Host only] All reserved item slot prices will scale with this value."));
            forceEnableThisModIfNotEnabledOnHost = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "ForceEnableThisModIfNotEnabledOnHost", false, "This is disabled by default for a reason, and it is NOT recommended to enable this, especially in public lobbies. Enabling this while the host does not have the ReservedItemSlotCore mod CAN, and likely WILL cause de-sync issues.\nNOTE: If enabled, you will not receive any reserved item slots until the game has started.\nThis setting only applies if you are a non-host client, and the host does not have this mod."));
            displayNegativePrioritySlotsLeftSideOfScreen = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "DisplayNegativePrioritySlotsLeftSideOfScreen", true, "For any reserved item slot mods that have a negative priority, by default, those slots will appear on the left side of the screen, rather than the right. Setting this option to false will have them appear on top of the slots on the right side."));
            hideEmptyReservedItemSlots = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "HideEmptyReservedItemSlots", false, "If true, all empty reserved item slots will be hidden from the HUD. This is a new config setting and might have bugs."));
            //toggleFocusReservedHotbar = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "ToggleFocusReservedHotbar", false, "If true, swapping to the reserved hotbar slots will be toggled when pressing the hotkey rather than while holding the hotkey.\nNOTE: This option only applies if you are NOT using the InputUtils mod."));
            //allowScrollingBetweenHotbars = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "AllowScrollingBetweenHotbars", false, "If true, you will be able to scroll between the default and reserved hotbars.\nThis will disable the hotkey for swapping between the two hotbars.\nNOTE: This will also disable fixing the scroll direction in the reserved hotbars to match the direction you scroll."));
            preventReservedItemSlotFade = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "PreventReservedHotbarSlotFade", false, "If true, the reserved hotbar slots will not fade with the rest of the default slots. Currently, the reserved hotbar slots will never fade while you're in the slots."));
            hideFocusHotbarTooltip = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "HideFocusHotbarTooltip", false, "This tooltip is shown next to the first reserved item slot."));

            showReservedItemsHolstered = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "ShowReservedItemsHolsteredOnPlayers", true, "If true, held items in the reserved item slots that are not currently in the player's hand, will be shown holstered on the player. Example: The reserved flashlight will show up on the player's shoulder when not in their hands. This will do nothing for items that do not have a defined holstered location on the player."));
            showReservedItemsHolsteredMaskedEnemy = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "ShowReservedItemsHolsteredOnMaskedEnemies", true, "If true, masked enemies will appear to have reserved items holstered when mimicking a player. The items shown will be the same items that the player, who the masked enemy is mimicking, has in their inventory. This will do nothing for items that do not have a defined holstered location on the player."));

            applyHotbarPlusItemSlotSize = AddConfigEntry(Plugin.instance.Config.Bind("Compatibility", "ApplyHotbarPlusItemSlotSize", true, "If true, the reserved item slot SIZE will be a adjusted by HotbarPlus's formatting settings.\nThis will not do anything if HotbarPlus is not enabled."));
            disableHotbarPlusEnergyBars = AddConfigEntry(Plugin.instance.Config.Bind("Compatibility", "DisableHotbarPlusEnergyBars", false, "Disables/hides the energy bars from HotbarPlus from the reserved item slots (HUD).\nThis will not do anything if HotbarPlus is not enabled."));

            numCustomItemSlots = AddConfigEntry(Plugin.instance.Config.Bind("Custom Reserved Item Slots", "NumCustomItemSlots", 0, "[Host only] Set the number of custom reserved item slots you want to add. Custom slots will update in the config when you start the game. LIMITED TO 10"));

            verboseLogs = AddConfigEntry(Plugin.instance.Config.Bind("Other", "VerboseLogs", true, "If enabled, extra logs will be created for debugging. This may be useful for tracking down various issues."));

            numCustomItemSlots.Value = Mathf.Clamp(numCustomItemSlots.Value, 0, 10);

            for (int i = 0; i < numCustomItemSlots.Value; i++)
            {
                ConfigEntry<string> customItemSlotName = AddConfigEntry(Plugin.instance.Config.Bind("CustomReservedItemSlot " + (i + 1), "ItemSlotName " + (i + 1), "custom_item_slot_" + (i + 1), "[Host only] Make the name of this slot unique. This name is usually only seen in the terminal. This slot will not be created if left blank."));
                ConfigEntry<string> customItemSlotItems = AddConfigEntry(Plugin.instance.Config.Bind("CustomReservedItemSlot " + (i + 1), "ItemsInSlot " + (i + 1), "", "[Host only] Syntax: \"Flashlight,Walkie-talkie\" (without quotes). When adding items, use the item's name as it appears in game. The names are CASE-SENSITIVE. Include spaces if there are spaces in the item name. Adding items that do not exist, or that are from a mod which is not enabled will not cause any problems. As of now, additional items added to reserved item slots cannot be seen on players while holstered."));
                ConfigEntry<int> customItemSlotPriority = AddConfigEntry(Plugin.instance.Config.Bind("CustomReservedItemSlot " + (i + 1), "ItemSlotPriority " + (i + 1), 20, "[Host only] Manually set the priority for this item slot. Higher priority slots will come first in the reserved item slots, which will appear below the other slots. Negative priority items will appear on the left side of the screen, this is disabled in the core mod's config."));
                ConfigEntry<int> customItemSlotPrice = AddConfigEntry(Plugin.instance.Config.Bind("CustomReservedItemSlot " + (i + 1), "ItemSlotPrice " + (i + 1), 0, "[Host only] Only applies if purchasing item slots in the terminal is enabled in the core config. Setting 0 will force this item to be unlocked immediately after the game starts."));

                var customConfigEntry = new CustomItemSlotConfigEntry(customItemSlotName.Value, ParseItemNames(customItemSlotItems.Value), customItemSlotPriority.Value, customItemSlotPrice.Value);

                if (customConfigEntry.customItemSlotName != "" && customConfigEntry.customItemSlotItems.Length > 0)
                    customItemSlotConfigs.Add(customConfigEntry);
            }

            TryRemoveOldConfigSettings();
        }


        public static string[] ParseItemNames(string itemNamesRaw)
        {
            if (itemNamesRaw == "")
                return new string[0];

            itemNamesRaw = itemNamesRaw.Replace(", ", ",");
            List<string> itemNames = new List<string>(itemNamesRaw.Split(','));
            itemNames = itemNames.Where(s => s.Length >= 1).ToList();
            return itemNames.ToArray();
        }


        public static ConfigEntry<T> AddConfigEntry<T>(ConfigEntry<T> configEntry)
        {
            currentConfigEntries.Add(configEntry.Definition.Key, configEntry);
            return configEntry;
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


    public class CustomItemSlotConfigEntry
    {
        public string customItemSlotName;
        public string[] customItemSlotItems;
        public int customItemSlotPriority;
        public int customItemSlotPrice;

        public CustomItemSlotConfigEntry(string customItemSlotName, string[] customItemSlotItems, int customItemSlotPriority, int customItemSlotPrice)
        {
            this.customItemSlotName = customItemSlotName;
            this.customItemSlotItems = customItemSlotItems;
            this.customItemSlotPriority = customItemSlotPriority;
            this.customItemSlotPrice = customItemSlotPrice;
        }
    }
}
