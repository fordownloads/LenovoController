using System.Globalization;
using System.Resources;

namespace LenovoController
{
    /// <summary>
    ///     UI strings resolved from the system UI language. There is intentionally no in-app
    ///     language switch; the resources follow Windows.
    /// </summary>
    public static class Localization
    {
        private static readonly ResourceManager Resources =
            new ResourceManager("LenovoController.Properties.Strings", typeof(Localization).Assembly);

        public static CultureInfo Culture => CultureInfo.CurrentUICulture;

        private static string Get(string name) => Resources.GetString(name, Culture) ?? name;

        public static string HeaderTitle => Get("HeaderTitle");

        public static string ChargingSection => Get("ChargingSection");
        public static string AlwaysOnUsbSection => Get("AlwaysOnUsbSection");
        public static string PowerModeSection => Get("PowerModeSection");

        public static string BatteryConservation => Get("BatteryConservation");
        public static string BatteryNormal => Get("BatteryNormal");
        public static string BatteryRapid => Get("BatteryRapid");

        public static string UsbOff => Get("UsbOff");
        public static string UsbOnSleep => Get("UsbOnSleep");
        public static string UsbAlways => Get("UsbAlways");

        public static string PowerModeQuiet => Get("PowerModeQuiet");
        public static string PowerModeBalanced => Get("PowerModeBalanced");
        public static string PowerModePerformance => Get("PowerModePerformance");

        public static string OverDrive => Get("OverDrive");
        public static string FnLock => Get("FnLock");
        public static string TouchpadLock => Get("TouchpadLock");

        public static string RefreshTooltip => Get("RefreshTooltip");
        public static string MoreTooltip => Get("MoreTooltip");

        public static string MenuOpen => Get("MenuOpen");
        public static string MenuRefresh => Get("MenuRefresh");
        public static string MenuExit => Get("MenuExit");

        public static string Autostart => Get("Autostart");
        public static string AutostartDescription => Get("AutostartDescription");
        public static string AutostartError => Get("AutostartError");

        public static string ErrorTitle => Get("ErrorTitle");

        public static string TrayTooltipFormat => Get("TrayTooltipFormat");
    }
}
