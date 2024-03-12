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
    [BepInPlugin("FlipMods.ReservedUtilitySlot", "ReservedUtilitySlot", "1.0.0")]
    [BepInDependency("FlipMods.ReservedItemSlotCore", BepInDependency.DependencyFlags.HardDependency)]
	public class Plugin : BaseUnityPlugin
	{
        public static Plugin instance;
        static ManualLogSource logger;
        Harmony _harmony;

        public static ReservedItemSlotData utilitySlotData;
        public static ReservedItemData ladderData;
        public static ReservedItemData lockpickerData;
        public static ReservedItemData jetpackData;
        public static ReservedItemData stunGrenadeData;
        public static ReservedItemData homemadeFlashbangData;
        public static ReservedItemData tzpInhalantData;
        public static ReservedItemData radarBoosterData;

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
        public static ReservedItemData toothpasteData;


        public static List<ReservedItemData> additionalItemData = new List<ReservedItemData>();

        void Awake()
        {
            instance = this;
            CreateCustomLogger();
            ConfigSettings.BindConfigSettings();

            CreateReservedItemSlots();
            CreateAdditionalReservedItemSlots();

            _harmony = new Harmony("ReservedUtilitySlot");
            PatchAll();
            Log("ReservedUtilitySlot loaded");
        }


        void CreateReservedItemSlots()
        {
            utilitySlotData = ReservedItemSlotData.CreateReservedItemSlotData("utility", 50, 250);

            // Set override values from config
            utilitySlotData.slotPriority = ConfigSettings.overrideItemSlotPriority.Value;
            utilitySlotData.purchasePrice = ConfigSettings.overridePurchasePrice.Value;

            ladderData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Extension ladder"));
            lockpickerData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Lockpicker"));
            jetpackData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Jetpack"));
            stunGrenadeData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Stun grenade"));
            homemadeFlashbangData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Homemade flashbang"));
            tzpInhalantData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("TZP-Inhalant"));
            radarBoosterData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Radar-booster"));

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
            toothpasteData = utilitySlotData.AddItemToReservedItemSlot(new ReservedItemData("Toothpaste"));
        }


        void CreateAdditionalReservedItemSlots()
        {
            string[] additionalItemNames = ConfigSettings.ParseAdditionalItems();
            foreach (string itemName in additionalItemNames)
            {
                if (!utilitySlotData.ContainsItem(itemName))
                {
                    LogWarning("Adding additional item to reserved item slot. Item: " + itemName);
                    var itemData = new ReservedItemData(itemName);
                    additionalItemData.Add(itemData);
                    utilitySlotData.AddItemToReservedItemSlot(itemData);
                }
            }


            string[] removeItemNamesFromSlot = ConfigSettings.ParseRemoveItems();
            foreach (string itemName in removeItemNamesFromSlot)
            {
                if (utilitySlotData.ContainsItem(itemName))
                {
                    LogWarning("Removing item from reserved item slot. Item: " + itemName);
                    utilitySlotData.RemoveItemFromReservedItemSlot(itemName);
                }
            }
        }


        void PatchAll()
        {
            IEnumerable<Type> types;
            try { types = Assembly.GetExecutingAssembly().GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null); }
            foreach (var type in types)
                this._harmony.PatchAll(type);
        }


        void CreateCustomLogger()
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
