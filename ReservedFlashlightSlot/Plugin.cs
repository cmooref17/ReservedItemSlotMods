using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using HarmonyLib;
using UnityEngine.SceneManagement;
using BepInEx.Logging;
using System.Reflection;
using UnityEngine;
using ReservedItemSlotCore;
using ReservedItemSlotCore.Data;
using ReservedFlashlightSlot.Input;
using ReservedFlashlightSlot.Config;

namespace ReservedFlashlightSlot
{
    [BepInPlugin("FlipMods.ReservedFlashlightSlot", "ReservedFlashlightSlot", "2.0.0")]
    [BepInDependency("FlipMods.ReservedItemSlotCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
		public static Plugin instance;
        static ManualLogSource logger;
		Harmony _harmony;

        public static ReservedItemSlotData flashlightSlotData;
        public static ReservedItemData flashlightData;
        public static ReservedItemData proFlashlightData;
        public static ReservedItemData laserPointerData;

        public static List<ReservedItemData> additionalItemData = new List<ReservedItemData>();

        void Awake()
        {
			instance = this;
            CreateCustomLogger();
			ConfigSettings.BindConfigSettings();

            CreateReservedItemSlots();
            CreateAdditionalReservedItemSlots();

            _harmony = new Harmony("ReservedFlashlightSlot");
			PatchAll();
			Log("ReservedFlashlightSlot loaded");
		}


        void CreateReservedItemSlots()
        {
            flashlightSlotData = ReservedItemSlotData.CreateReservedItemSlotData("flashlight", 120, 200);

            // Set override values from config
            flashlightSlotData.slotPriority = ConfigSettings.overrideItemSlotPriority.Value;
            flashlightSlotData.purchasePrice = ConfigSettings.overridePurchasePrice.Value;

            flashlightData = flashlightSlotData.AddItemToReservedItemSlot(new ReservedItemData("Flashlight", PlayerBone.Spine3, new Vector3(0.2f, 0.25f, 0), new Vector3(90, 0, 0)));
            proFlashlightData = flashlightSlotData.AddItemToReservedItemSlot(new ReservedItemData("Pro-flashlight", PlayerBone.Spine3, new Vector3(0.2f, 0.25f, 0), new Vector3(90, 0, 0)));
            laserPointerData = flashlightSlotData.AddItemToReservedItemSlot(new ReservedItemData("Laser pointer", PlayerBone.Spine3, new Vector3(0.2f, 0.25f, 0), new Vector3(90, 0, 0)));
        }

        
        void CreateAdditionalReservedItemSlots()
        {
            string[] additionalItemNames = ConfigSettings.ParseAdditionalItems();
            foreach (string itemName in additionalItemNames)
            {
                if (!flashlightSlotData.ContainsItem(itemName))
                {
                    LogWarning("Adding additional item to reserved item slot. Item: " + itemName);
                    var itemData = new ReservedItemData(itemName);
                    additionalItemData.Add(itemData);
                    flashlightSlotData.AddItemToReservedItemSlot(itemData);
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
