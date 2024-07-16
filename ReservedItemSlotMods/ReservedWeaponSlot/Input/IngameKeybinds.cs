using LethalCompanyInputUtils.Api;
using UnityEngine.InputSystem;

namespace ReservedWeaponSlot.Input
{
    internal class IngameKeybinds : LcInputActions
    {
        internal static IngameKeybinds Instance = new IngameKeybinds();
        internal static InputActionAsset GetAsset() => Instance.Asset;

        [InputAction("<Keyboard>/t", Name = "[ReservedItemSlots]\nToggle Weapon Slot")]
        public InputAction ToggleWeaponSlotHotkey { get; set; }
    }
}
