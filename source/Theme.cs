using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace LenovoController
{
    /// <summary>
    ///     Reads the Windows personalisation theme and keeps the app's palette in sync with it.
    /// </summary>
    internal static class Theme
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AccentKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";

        internal static bool IsLight { get; private set; } = true;

        internal static bool IsTaskbarLight { get; private set; } = true;

        internal static event EventHandler Changed;

        internal static void Refresh()
        {
            IsLight = ReadDword("AppsUseLightTheme", true);
            IsTaskbarLight = ReadDword("SystemUsesLightTheme", true);

            if (Application.Current == null)
                return;

            SetBrush("TextPrimaryBrush", IsLight ? "#FF1B1B1B" : "#FFFFFFFF");
            SetBrush("TextSecondaryBrush", IsLight ? "#99000000" : "#B3FFFFFF");
            SetBrush("StrokeBrush", IsLight ? "#14000000" : "#1FFFFFFF");
            SetBrush("SegmentBackgroundBrush", IsLight ? "#0A000000" : "#10FFFFFF");
            SetBrush("HoverBrush", IsLight ? "#14000000" : "#22FFFFFF");
            SetBrush("DividerBrush", IsLight ? "#14000000" : "#18FFFFFF");
            SetBrush("IconButtonHoverBrush", IsLight ? "#10000000" : "#1AFFFFFF");
            SetBrush("WindowFallbackBrush", IsLight ? "#FFF3F3F3" : "#FF202020");

            var accent = ReadAccentColor();
            SetBrush("AccentBrush", accent);
            SetBrush("AccentTextBrush", GetContrastColor(accent));

            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static Color ReadAccentColor()
        {
            var palette = ReadAccentPalette();
            if (palette != null)
            {
                // Windows stores eight accent shades. The base colour is the fourth entry;
                // the second entry is its lightened dark-mode variant.
                var color = IsLight ? palette[3] : palette[1];
                if (color.A != 0)
                    return color;
            }

            return ReadAccentMenuColor() ?? Color.FromRgb(0x00, 0x78, 0xD4);
        }

        private static Color[] ReadAccentPalette()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AccentKey))
                {
                    if (!(key?.GetValue("AccentPalette") is byte[] palette) || palette.Length < 32)
                        return null;

                    var colors = new Color[8];
                    for (var i = 0; i < colors.Length; i++)
                        colors[i] = Color.FromRgb(palette[i * 4], palette[i * 4 + 1], palette[i * 4 + 2]);

                    return colors;
                }
            }
            catch
            {
                return null;
            }
        }

        private static Color? ReadAccentMenuColor()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AccentKey))
                {
                    if (key?.GetValue("AccentColorMenu") is int value)
                    {
                        return Color.FromRgb(
                            (byte) (value & 0xFF),
                            (byte) ((value >> 8) & 0xFF),
                            (byte) ((value >> 16) & 0xFF));
                    }
                }
            }
            catch
            {
                // Ignored; the default accent is used below.
            }

            return null;
        }

        private static Color GetContrastColor(Color background)
        {
            return GetRelativeLuminance(background) > 0.4
                ? Color.FromRgb(0x00, 0x00, 0x00)
                : Color.FromRgb(0xFF, 0xFF, 0xFF);
        }

        private static double GetRelativeLuminance(Color color)
        {
            return 0.2126 * Linearize(color.R) +
                   0.7152 * Linearize(color.G) +
                   0.0722 * Linearize(color.B);
        }

        private static double Linearize(byte value)
        {
            var channel = value / 255.0;
            return channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        private static void SetBrush(string key, string color)
        {
            SetBrush(key, (Color) ColorConverter.ConvertFromString(color));
        }

        private static void SetBrush(string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            Application.Current.Resources[key] = brush;
        }

        private static bool ReadDword(string name, bool fallback)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    var value = key?.GetValue(name);
                    return value == null ? fallback : Convert.ToInt32(value) != 0;
                }
            }
            catch
            {
                return fallback;
            }
        }
    }
}
