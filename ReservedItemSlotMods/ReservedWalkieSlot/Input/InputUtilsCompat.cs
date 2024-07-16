using BepInEx.Bootstrap;
using LethalCompanyInputUtils.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

namespace ReservedWalkieSlot.Input
{
    internal class InputUtilsCompat
    {
        internal static InputActionAsset Asset { get { return IngameKeybinds.GetAsset(); } }
        internal static bool Enabled => Plugin.IsModLoaded("com.rune580.LethalCompanyInputUtils");

        public static InputAction ActivateWalkieHotkey => IngameKeybinds.Instance.ActivateWalkieHotkey;
    }
}
