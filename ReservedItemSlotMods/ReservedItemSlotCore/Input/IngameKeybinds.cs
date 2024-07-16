using BepInEx.Bootstrap;
using LethalCompanyInputUtils.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

namespace ReservedItemSlotCore.Input
{
    internal class IngameKeybinds : LcInputActions
    {
        internal static IngameKeybinds Instance;
        internal static InputActionAsset GetAsset() => Instance.Asset;

        [InputAction("<Keyboard>/leftAlt", GamepadPath = "<Gamepad>/leftShoulder", Name = "Swap hotbars")]
        internal InputAction FocusReservedHotbarHotkey { get; set; }
    }
}