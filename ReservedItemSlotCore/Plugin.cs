using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using ReservedItemSlotCore.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Security.Cryptography;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices.ComTypes;
using ReservedItemSlotCore.Config;
using ReservedItemSlotCore.Input;
using BepInEx.Logging;
using System.Reflection;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using ReservedItemSlotCore.Data;


namespace ReservedItemSlotCore
{
	[BepInPlugin("FlipMods.ReservedItemSlotCore", "ReservedItemSlotCore", "2.0.20")]
    [BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
    internal class Plugin : BaseUnityPlugin
    {
		Harmony _harmony;
		public static Plugin instance;
        private static ManualLogSource logger;

        public static List<ReservedItemSlotData> customItemSlots = new List<ReservedItemSlotData>();
       

        private void Awake()
        {
			instance = this;
            CreateCustomLogger();
            ConfigSettings.BindConfigSettings();

            AddCustomItemSlots();

            if (InputUtilsCompat.Enabled)
                InputUtilsCompat.Init();

			this._harmony = new Harmony("ReservedItemSlotCore");
            PatchAll();
            Log("ReservedItemSlotCore loaded");
		}


        private void AddCustomItemSlots()
        {
            foreach (var customItemSlotConfig in ConfigSettings.customItemSlotConfigs)
            {
                if (customItemSlotConfig.customItemSlotName == "" || customItemSlotConfig.customItemSlotItems.Length <= 0)
                    continue;

                var customItemSlot = ReservedItemSlotData.CreateReservedItemSlotData(customItemSlotConfig.customItemSlotName, customItemSlotConfig.customItemSlotPriority, customItemSlotConfig.customItemSlotPrice);
                foreach (var itemName in customItemSlotConfig.customItemSlotItems)
                {
                    var customItemData = new ReservedItemData(itemName);
                    customItemSlot.AddItemToReservedItemSlot(customItemData);
                    customItemSlots.Add(customItemSlot);
                }
            }
        }


        private void PatchAll()
        {
            IEnumerable<Type> types;
            try
            {
                types = Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null);
            }
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
