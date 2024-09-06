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
using ReservedSprayPaintSlot.Config;

namespace ReservedSprayPaintSlot
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("FlipMods.ReservedItemSlotCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;
        static ManualLogSource logger;
        Harmony _harmony;

        public static ReservedItemSlotData sprayPaintSlotData;
        public static ReservedItemData sprayPaintData;

        public static List<ReservedItemData> additionalItemData = new List<ReservedItemData>();


        void Awake()
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


        void CreateReservedItemSlots()
        {
            sprayPaintSlotData= ReservedItemSlotData.CreateReservedItemSlotData("spray_paint", ConfigSettings.overrideItemSlotPriority.Value, ConfigSettings.overridePurchasePrice.Value);
            sprayPaintData = sprayPaintSlotData.AddItemToReservedItemSlot(new ReservedItemData("Spray paint", PlayerBone.Hips, new Vector3(0.26f, -0.05f, 0.2f), new Vector3(-105, 0, 0)));
        }


        void CreateAdditionalReservedItemSlots()
        {
            string[] additionalItemNames = ConfigSettings.ParseAdditionalItems();
            foreach (string itemName in additionalItemNames)
            {
                if (!sprayPaintSlotData.ContainsItem(itemName))
                {
                    LogWarning("Adding additional item to reserved item slot. Item: " + itemName);
                    var itemData = new ReservedItemData(itemName);
                    additionalItemData.Add(itemData);
                    sprayPaintSlotData.AddItemToReservedItemSlot(itemData);
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
            try { logger = BepInEx.Logging.Logger.CreateLogSource(string.Format("{0}-{1}", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)); }
            catch { logger = Logger; }
        }

        public static void Log(string message) => logger.LogInfo(message);
        public static void LogError(string message) => logger.LogError(message);
        public static void LogWarning(string message) => logger.LogWarning(message);

        public static bool IsModLoaded(string guid) => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid);
    }
}
