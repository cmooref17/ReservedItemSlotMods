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
using ReservedWalkieSlot.Input;
using ReservedWalkieSlot.Config;

namespace ReservedWalkieSlot
{
    [BepInPlugin("FlipMods.ReservedWalkieSlot", "ReservedWalkieSlot", "2.0.0")]
    [BepInDependency("FlipMods.ReservedItemSlotCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;
        static ManualLogSource logger;
        Harmony _harmony;

        public static ReservedItemSlotData walkieSlotData;
        public static ReservedItemData walkieData;

        public static List<ReservedItemData> additionalItemData = new List<ReservedItemData>();


        void Awake()
        {
            instance = this;
            CreateCustomLogger();
            ConfigSettings.BindConfigSettings();

            CreateReservedItemSlots();
            CreateAdditionalReservedItemSlots();

            _harmony = new Harmony("ReservedWalkieSlot");
            PatchAll();
            Log("ReservedWalkieSlot loaded");
        }


        void CreateReservedItemSlots()
        {
            walkieSlotData = ReservedItemSlotData.CreateReservedItemSlotData("walkie_talkie", ConfigSettings.overrideItemSlotPriority.Value, ConfigSettings.overridePurchasePrice.Value);
            walkieData = walkieSlotData.AddItemToReservedItemSlot(new ReservedItemData("Walkie-talkie", PlayerBone.Spine3, new Vector3(0.15f, -0.05f, 0.25f), new Vector3(0, -90, 100)));
        }


        void CreateAdditionalReservedItemSlots()
        {
            string[] additionalItemNames = ConfigSettings.ParseAdditionalItems();
            foreach (string itemName in additionalItemNames)
            {
                if (!walkieSlotData.ContainsItem(itemName))
                {
                    LogWarning("Adding additional item to reserved item slot. Item: " + itemName);
                    var itemData = new ReservedItemData(itemName);
                    additionalItemData.Add(itemData);
                    walkieSlotData.AddItemToReservedItemSlot(itemData);
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
