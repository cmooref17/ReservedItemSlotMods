using UnityEngine.InputSystem;

namespace ReservedItemSlotCore.Input
{
    internal class InputUtilsCompat
    {
        internal static bool Enabled => Plugin.IsModLoaded("com.rune580.LethalCompanyInputUtils");
        internal static InputActionAsset Asset { get { return IngameKeybinds.Instance.Asset; } }
        public static InputAction FocusReservedHotbarHotkey => IngameKeybinds.Instance.FocusReservedHotbarHotkey;
        internal static void Init()
        {
            if (Enabled && IngameKeybinds.Instance == null)
                IngameKeybinds.Instance = new IngameKeybinds();
        }
    }
}
