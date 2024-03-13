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
using ReservedWeaponSlot.Config;


namespace ReservedWeaponSlot
{
    [BepInPlugin("FlipMods.ReservedWeaponSlot", "ReservedWeaponSlot", "1.1.1")]
    [BepInDependency("FlipMods.ReservedItemSlotCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
	{
        public static Plugin instance;
        static ManualLogSource logger;
        Harmony _harmony;

        public static ReservedItemSlotData weaponSlotData;
        public static ReservedItemSlotData rangedWeaponSlotData;
        public static ReservedItemSlotData ammoSlotData;

        public static ReservedItemData shotgunData;
        public static ReservedItemData zapGunData;

		public static ReservedItemData shovelData;
		public static ReservedItemData stopSignData;
		public static ReservedItemData yieldSignData;

        //public static ReservedItemData stunGrenadeData;// = new ReservedItemData("Stun grenade", 70);
        //public static ReservedItemData homemadeFlashbangData;// = new ReservedItemData("Homemade flashbang", 70);

        public static ReservedItemData rocketLauncherData;
        public static ReservedItemData flareGunData;
        public static ReservedItemData toyGunData;
		public static ReservedItemData toyHammerData;

        public static ReservedItemData goldenShovelData;

        public static ReservedItemData ammoData;
		public static ReservedItemData shotgunAmmoData;
		public static ReservedItemData flareGunAmmoData;


        public static List<ReservedItemData> additionalItemData = new List<ReservedItemData>();

        void Awake()
        {
            instance = this;
            CreateCustomLogger();
            ConfigSettings.BindConfigSettings();

            CreateReservedItemSlots();
            CreateAdditionalReservedItemSlots();

            _harmony = new Harmony("ReservedWeaponSlot");
            PatchAll();
            Log("ReservedWeaponSlot loaded");
        }


        void CreateReservedItemSlots()
        {
            weaponSlotData = ReservedItemSlotData.CreateReservedItemSlotData("weapons", ConfigSettings.overrideMeleeSlotPriority.Value, ConfigSettings.overrideMeleeSlotPrice.Value);
            
            if (ConfigSettings.combineMeleeAndRangedWeaponSlots.Value)
            {
                rangedWeaponSlotData = weaponSlotData;
                weaponSlotData.purchasePrice = ConfigSettings.overrideCombinedWeaponSlotPrice.Value;
            }
            else
            {
                weaponSlotData.slotName = "melee_weapons";
                rangedWeaponSlotData = ReservedItemSlotData.CreateReservedItemSlotData("ranged_weapons", ConfigSettings.overrideRangedSlotPriority.Value, ConfigSettings.overrideRangedSlotPrice.Value);
            }

            shovelData = weaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Shovel"));
            stopSignData = weaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Stop sign"));
            yieldSignData = weaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Yield sign"));
            toyGunData = weaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Toy Hammer"));
            goldenShovelData = weaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Australium Shovel"));


            shotgunData = rangedWeaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Shotgun"));
            zapGunData = rangedWeaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Zap gun"));
            rocketLauncherData = rangedWeaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Rocket Launcher"));
            flareGunData = rangedWeaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Flaregun"));
            toyGunData = rangedWeaponSlotData.AddItemToReservedItemSlot(new ReservedItemData("Revolver"));
            

            ammoSlotData = null;
            if (!ConfigSettings.disableReservedAmmoSlot.Value)
            {
                ammoSlotData = ReservedItemSlotData.CreateReservedItemSlotData("ammo", ConfigSettings.overrideAmmoSlotPriority.Value, ConfigSettings.overrideAmmoSlotPrice.Value);
                ammoData = ammoSlotData.AddItemToReservedItemSlot(new ReservedItemData("Ammo"));
                shotgunAmmoData = ammoSlotData.AddItemToReservedItemSlot(new ReservedItemData("Shells"));
                flareGunAmmoData = ammoSlotData.AddItemToReservedItemSlot(new ReservedItemData("Emergency Flare (ammo)"));
            }
        }


        void CreateAdditionalReservedItemSlots()
        {
            string[] additionalItemNames = ConfigSettings.ParseAdditionalMeleeWeaponItems();
            foreach (string itemName in additionalItemNames)
            {
                if (!weaponSlotData.ContainsItem(itemName))
                {
                    LogWarning("Adding additional item to reserved (melee weapons) item slot. Item: " + itemName);
                    var itemData = new ReservedItemData(itemName);
                    additionalItemData.Add(itemData);
                    weaponSlotData.AddItemToReservedItemSlot(itemData);
                }
            }

            additionalItemNames = ConfigSettings.ParseAdditionalRangedWeaponItems();
            foreach (string itemName in additionalItemNames)
            {
                if (!rangedWeaponSlotData.ContainsItem(itemName))
                {
                    LogWarning("Adding additional item to reserved (ranged weapons) item slot. Item: " + itemName);
                    var itemData = new ReservedItemData(itemName);
                    additionalItemData.Add(itemData);
                    rangedWeaponSlotData.AddItemToReservedItemSlot(itemData);
                }
            }

            if (!ConfigSettings.disableReservedAmmoSlot.Value)
            {
                additionalItemNames = ConfigSettings.ParseAdditionalAmmoItems();
                foreach (string itemName in additionalItemNames)
                {
                    if (!ammoSlotData.ContainsItem(itemName))
                    {
                        LogWarning("Adding additional item to reserved (ammo) item slot. Item: " + itemName);
                        var itemData = new ReservedItemData(itemName);
                        additionalItemData.Add(itemData);
                        ammoSlotData.AddItemToReservedItemSlot(itemData);
                    }
                }
            }


            string[] removeItemNames = ConfigSettings.ParseRemoveMeleeWeaponItems();
            foreach (string itemName in removeItemNames)
            {
                if (!weaponSlotData.ContainsItem(itemName))
                {
                    LogWarning("Removing item from reserved (melee weapons) item slot. Item: " + itemName);
                    weaponSlotData.RemoveItemFromReservedItemSlot(itemName);
                }
            }

            removeItemNames = ConfigSettings.ParseRemoveRangedWeaponItems();
            foreach (string itemName in removeItemNames)
            {
                if (!rangedWeaponSlotData.ContainsItem(itemName))
                {
                    LogWarning("Removing item from reserved (ranged weapons) item slot. Item: " + itemName);
                    rangedWeaponSlotData.RemoveItemFromReservedItemSlot(itemName);
                }
            }

            if (ammoData != null)
            {
                removeItemNames = ConfigSettings.ParseRemoveAmmoItems();
                foreach (string itemName in removeItemNames)
                {
                    if (!ammoSlotData.ContainsItem(itemName))
                    {
                        LogWarning("Removing item from reserved (ammo) item slot. Item: " + itemName);
                        ammoSlotData.RemoveItemFromReservedItemSlot(itemName);
                    }
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
