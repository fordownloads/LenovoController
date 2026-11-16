using System;
using LenovoController.Features;
using Microsoft.Win32;

namespace LenovoController
{
    internal enum TrayClickAction
    {
        OpenPopup,
        SwitchPowerModes
    }

    /// <summary>
    ///     Small per-user settings store for tray behaviour.
    /// </summary>
    internal static class AppSettings
    {
        private const string KeyPath = @"Software\LenovoController";
        private const int AllPowerModes = 0x7;

        internal static TrayClickAction SingleClickAction
        {
            get
            {
                var value = ReadDword("SingleClickAction", 0);
                return value == 1 ? TrayClickAction.SwitchPowerModes : TrayClickAction.OpenPopup;
            }
            set => WriteDword("SingleClickAction", value == TrayClickAction.SwitchPowerModes ? 1 : 0);
        }

        internal static bool IsPowerModeEnabled(PowerModeState mode)
        {
            return (ToggleModes & (1 << (int) mode)) != 0;
        }

        internal static bool SetPowerModeEnabled(PowerModeState mode, bool enabled)
        {
            var mask = ToggleModes;
            var bit = 1 << (int) mode;

            if (enabled)
                mask |= bit;
            else
                mask &= ~bit;

            // Keep at least one mode in the rotation.
            if (mask == 0)
                return false;

            WriteDword("ToggleModes", mask);
            return true;
        }

        internal static PowerModeState[] GetToggleModes()
        {
            var mask = ToggleModes;
            var modes = new System.Collections.Generic.List<PowerModeState>(3);

            foreach (PowerModeState mode in Enum.GetValues(typeof(PowerModeState)))
            {
                if ((mask & (1 << (int) mode)) != 0)
                    modes.Add(mode);
            }

            return modes.ToArray();
        }

        private static int ToggleModes
        {
            get
            {
                var value = ReadDword("ToggleModes", AllPowerModes) & AllPowerModes;
                return value == 0 ? AllPowerModes : value;
            }
        }

        private static int ReadDword(string name, int fallback)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    var value = key?.GetValue(name);
                    return value == null ? fallback : Convert.ToInt32(value);
                }
            }
            catch
            {
                return fallback;
            }
        }

        private static void WriteDword(string name, int value)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    key?.SetValue(name, value, RegistryValueKind.DWord);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.TraceError("Could not save setting: " + e);
            }
        }
    }
}
