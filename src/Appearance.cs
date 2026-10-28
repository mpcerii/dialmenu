using System;
using Microsoft.Win32;

namespace DialMenu
{
    // Erkennt, ob Windows gerade hell oder dunkel eingestellt ist, und
    // meldet Wechsel zur Laufzeit.
    public static class Appearance
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string Value = "AppsUseLightTheme";

        // "auto" folgt Windows, sonst gilt die feste Wahl.
        public static bool IsLight(string setting)
        {
            string mode = (setting ?? "auto").ToLowerInvariant();
            if (mode == "light") return true;
            if (mode == "dark") return false;
            return SystemIsLight();
        }

        public static bool SystemIsLight()
        {
            try
            {
                object value = Registry.GetValue(
                    Registry.CurrentUser.Name + "\\" + Key, Value, null);
                if (value is int) return (int)value != 0;
            }
            catch { }

            // Ohne Auskunft lieber dunkel: das ist die Voreinstellung von Windows.
            return false;
        }

        public static string Label(string setting)
        {
            string mode = (setting ?? "auto").ToLowerInvariant();
            if (mode == "light") return Strings.Get("appearance.modeLight");
            if (mode == "dark") return Strings.Get("appearance.modeDark");
            return Strings.Get("appearance.modeAuto");
        }
    }
}
