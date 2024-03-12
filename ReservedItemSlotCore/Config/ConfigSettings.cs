using BepInEx.Configuration;
using ReservedItemSlotCore.Networking;
using System.Collections.Generic;
using System.IO;


namespace ReservedItemSlotCore.Config
{
    public static class ConfigSettings
    {
        public static ConfigEntry<bool> disablePurchasingReservedSlots;
        public static ConfigEntry<float> globalItemSlotPriceModifier;
        public static ConfigEntry<bool> forceEnableThisModIfNotEnabledOnHost;
        public static ConfigEntry<bool> displayNegativePrioritySlotsLeftSideOfScreen;
        public static ConfigEntry<string> focusReservedHotbarHotkey;
        public static ConfigEntry<bool> toggleFocusReservedHotbar;
        public static ConfigEntry<bool> preventReservedItemSlotFade;

        public static Dictionary<string, ConfigEntryBase> currentConfigEntries = new Dictionary<string, ConfigEntryBase>();

        public static void BindConfigSettings()
        {
            Plugin.Log("BindingConfigs");

            disablePurchasingReservedSlots = AddConfigEntry(Plugin.instance.Config.Bind("Server", "DisablePurchasingReservedSlots", false, "[Host only] By default, you will have to unlock reserved item slots by purchasing them from the terminal. Setting this to true will disable this, and players will start the game with all available reserved item slots."));
            globalItemSlotPriceModifier = AddConfigEntry(Plugin.instance.Config.Bind("Server", "GlobalItemSlotPriceModifier", 1f, "[Host only] All reserved item slot prices will scale with this value."));
            forceEnableThisModIfNotEnabledOnHost = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "ForceEnableThisModIfNotEnabledOnHost", false, "This is disabled by default for a reason, and it is NOT recommended to enable this, especially in public lobbies. Enabling this when the host does not have the ReservedItemSlotCore mod CAN, and likely WILL cause de-sync issues. You have been warned. This setting only applies if you are a non-host client, and the host does not have this mod."));
            displayNegativePrioritySlotsLeftSideOfScreen = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "DisplayNegativePrioritySlotsLeftSideOfScreen", true, "For any reserved item slot mods that have a negative priority, by default, those slots will appear on the left side of the screen, rather than the right. Setting this option to false will have them appear on top of the slots on the right side."));
            focusReservedHotbarHotkey = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "FocusReservedItemSlotsHotkey", "<Keyboard>/leftAlt", "This setting will be ignored if InputUtils is installed and enabled. (I recommend running InputUtils to edit keybinds in the in-game settings)"));
            toggleFocusReservedHotbar = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "ToggleFocusReservedHotbar", false, "If set to true, swapping to the reserved hotbar slots will be toggled when pressing the hotkey rather than while holding the hotkey. Setting this option to true may have bugs at this current time."));
            preventReservedItemSlotFade = AddConfigEntry(Plugin.instance.Config.Bind("Client-side", "PreventReservedHotbarSlotFade", false, "If true, the reserved hotbar slots will not fade with the rest of the default slots."));

            TryRemoveOldConfigSettings();
            //ConfigSync.BuildDefaultConfigSync();
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
}
