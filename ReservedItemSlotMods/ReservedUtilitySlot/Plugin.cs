using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReservedItemSlotCore;
using UnityEngine;
using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using System.Reflection;
using ReservedItemSlotCore.Data;
using ReservedUtilitySlot.Config;
using UnityEngine.Rendering;


namespace ReservedUtilitySlot
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("FlipMods.ReservedItemSlotCore", BepInDependency.DependencyFlags.HardDependency)]
	public class Plugin : BaseUnityPlugin
	{
        public static Plugin instance;
        static ManualLogSource logger;
        Harmony _harmony;

        public static ReservedItemSlotData mainUtilitySlotData;
        public static List<ReservedItemSlotData> allUtilitySlotData = new List<ReservedItemSlotData>();

        public static ReservedItemData ladderData;
        public static ReservedItemData lockpickerData;
        public static ReservedItemData jetpackData;
        public static ReservedItemData stunGrenadeData;
        public static ReservedItemData homemadeFlashbangData;
        public static ReservedItemData tzpInhalantData;
        public static ReservedItemData radarBoosterData;
        public static ReservedItemData weedKillerData;

        // LethalThings
        public static ReservedItemData remoteRadarData;
        public static ReservedItemData utilityBeltData;
        public static ReservedItemData hackingToolData;
        public static ReservedItemData pingerData;

        // LateGameUpgrades
        public static ReservedItemData portableTeleData;
        public static ReservedItemData advancedPortableTeleData;
        public static ReservedItemData peeperData;
        public static ReservedItemData medkitData;

        public static ReservedItemData binocularsData;
        public static ReservedItemData mapperData;
        //public static ReservedItemData toothpasteData;

        public static List<ReservedItemData> additionalItemData = new List<ReservedItemData>();


        public static ReservedItemSlotData keySlotData;
        public static ReservedItemData keyData;

        public static List<ReservedItemData> additionalKeyItemData = new List<ReservedItemData>();


        private void Awake()
        {
            instance = this;
            CreateCustomLogger();
            ConfigSettings.BindConfigSettings();

            CreateReservedItemSlots();
            CreateAdditionalReservedItemSlots();

            _harmony = new Harmony(PluginInfo.PLUGIN_NAME);
            PatchAll();
            Log(PluginInfo.PLUGIN_NAME + " loaded");
        }


        private void CreateReservedItemSlots()
        {
            if (!ConfigSettings.disableUtilitySlot.Value)
            {
                for (int i = 0; i < ConfigSettings.numUtilitySlots.Value; i++)
                {
                    string slotName = "utility" + (i > 0 ? (i + 1).ToString() : "");
                    var utilitySlotData = ReservedItemSlotData.CreateReservedItemSlotData(slotName, ConfigSettings.overrideItemSlotPriority.Value + i, Mathf.Max(ConfigSettings.overridePurchasePrice.Value + ConfigSettings.overrideExtraAmmoSlotPriceIncrease.Value * i, 0));
                    if (mainUtilitySlotData == null)
                        mainUtilitySlotData = utilitySlotData;

                    if (ladderData == null)
                    {
                        ladderData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Extension ladder"));
                        jetpackData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Jetpack"));
                        stunGrenadeData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Stun grenade"));
                        homemadeFlashbangData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Homemade flashbang"));
                        tzpInhalantData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("TZP-Inhalant"));
                        radarBoosterData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Radar-booster"));
                        lockpickerData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Lockpicker"));
                        weedKillerData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Weed killer"));

                        remoteRadarData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Remote Radar"));
                        utilityBeltData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Utility Belt"));
                        hackingToolData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Hacking Tool"));
                        pingerData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Pinger"));

                        portableTeleData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Portable Tele"));
                        advancedPortableTeleData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Advanced Portable Tele"));
                        peeperData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Peeper"));
                        medkitData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Medkit"));

                        binocularsData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Binoculars"));
                        mapperData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Mapper"));

                        // You will be missed toothpaste slot
                        //toothpasteData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Toothpaste"));
                    }
                    else
                    {
                        utilitySlotData.AddItemToReservedItemSlot(ladderData);
                        utilitySlotData.AddItemToReservedItemSlot(jetpackData);
                        utilitySlotData.AddItemToReservedItemSlot(stunGrenadeData);
                        utilitySlotData.AddItemToReservedItemSlot(homemadeFlashbangData);
                        utilitySlotData.AddItemToReservedItemSlot(tzpInhalantData);
                        utilitySlotData.AddItemToReservedItemSlot(radarBoosterData);
                        utilitySlotData.AddItemToReservedItemSlot(lockpickerData);
                        utilitySlotData.AddItemToReservedItemSlot(weedKillerData);
                        
                        utilitySlotData.AddItemToReservedItemSlot(remoteRadarData);
                        utilitySlotData.AddItemToReservedItemSlot(utilityBeltData);
                        utilitySlotData.AddItemToReservedItemSlot(hackingToolData);
                        utilitySlotData.AddItemToReservedItemSlot(pingerData);
                        
                        utilitySlotData.AddItemToReservedItemSlot(portableTeleData);
                        utilitySlotData.AddItemToReservedItemSlot(advancedPortableTeleData);
                        utilitySlotData.AddItemToReservedItemSlot(peeperData);
                        utilitySlotData.AddItemToReservedItemSlot(medkitData);
                        
                        utilitySlotData.AddItemToReservedItemSlot(binocularsData);
                        utilitySlotData.AddItemToReservedItemSlot(mapperData);
                    }

                    allUtilitySlotData.Add(utilitySlotData);
                }
            }
            
            if (ConfigSettings.addKeySlot.Value)
            {
                keySlotData = ReservedItemSlotData.CreateReservedItemSlotData("key_slot", ConfigSettings.overrideKeySlotPriority.Value, ConfigSettings.overrideKeySlotPrice.Value);
                keyData = keySlotData.AddItemToReservedItemSlot(new ReservedItemData("Key"));

                if (ConfigSettings.moveLockpickerToKeySlot.Value)
                {
                    if (!ConfigSettings.disableUtilitySlot.Value)
                    {
                        keySlotData.AddItemToReservedItemSlot(lockpickerData);
                        mainUtilitySlotData.RemoveItemFromReservedItemSlot(lockpickerData.itemName);
                    }
                    else
                        keySlotData.AddItemToReservedItemSlot(new ReservedItemData("Lockpicker"));
                }
            }
        }


        private void CreateAdditionalReservedItemSlots()
        {
            string[] additionalItemNames = ConfigSettings.ParseAdditionalItems();
            foreach (string itemName in additionalItemNames)
            {
                if (!mainUtilitySlotData.ContainsItem(itemName))
                {
                    LogWarning("Adding additional item to reserved item slot. Item: " + itemName);
                    var itemData = new ReservedItemData(itemName);
                    additionalItemData.Add(itemData);
                    foreach (var utilitySlotData in allUtilitySlotData)
                    {
                        if (!utilitySlotData.ContainsItem(itemName))
                            utilitySlotData.AddItemToReservedItemSlot(itemData);
                    }
                }
            }


            string[] removeItemNamesFromSlot = ConfigSettings.ParseRemoveItems();
            foreach (string itemName in removeItemNamesFromSlot)
            {
                if (mainUtilitySlotData.ContainsItem(itemName))
                {
                    LogWarning("Removing item from reserved item slot. Item: " + itemName);
                    foreach (var utilitySlotData in allUtilitySlotData)
                    {
                        if (utilitySlotData.ContainsItem(itemName))
                            utilitySlotData.RemoveItemFromReservedItemSlot(itemName);
                    }
                }
            }


            if (keySlotData != null)
            {
                string[] additionalKeyItemNames = ConfigSettings.ParseAdditionalKeyItems();
                foreach (string itemName in additionalKeyItemNames)
                {
                    if (!keySlotData.ContainsItem(itemName))
                    {
                        LogWarning("Adding additional item to reserved key item slot. Item: " + itemName);
                        var itemData = new ReservedItemData(itemName);
                        additionalKeyItemData.Add(itemData);
                        keySlotData.AddItemToReservedItemSlot(itemData);
                    }
                }
            }
        }


        private void PatchAll()
        {
            IEnumerable<Type> types;
            try { types = Assembly.GetExecutingAssembly().GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null); }
            foreach (var type in types)
                this._harmony.PatchAll(type);
        }


        private void CreateCustomLogger()
        {
            try { logger = BepInEx.Logging.Logger.CreateLogSource(string.Format("{0}-{1}", Info.Metadata.Name, Info.Metadata.Version)); }
            catch { logger = Logger; }
        }

        public static void Log(string message) => logger.LogInfo(message);
        public static void LogError(string message) => logger.LogError(message);
        public static void LogWarning(string message) => logger.LogWarning(message);

        public static bool IsModLoaded(string guid) => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid);
    }
}
