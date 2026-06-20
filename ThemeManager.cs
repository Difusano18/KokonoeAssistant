using System;
using System.Windows.Media;
using System.Linq;
using KokonoeAssistant.Services;

namespace KokonoeAssistant
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string hexColor)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hexColor) || !hexColor.StartsWith("#"))
                    hexColor = "#6366F1";
                if (hexColor.Length == 4)
                    hexColor = $"#{hexColor[1]}{hexColor[1]}{hexColor[2]}{hexColor[2]}{hexColor[3]}{hexColor[3]}";
                if (hexColor.Length == 6)
                    hexColor = "#FF" + hexColor.Substring(1);
                
                System.Windows.Media.Color baseColor;
                try { baseColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor); }
                catch { baseColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6366F1"); }

                var white = System.Windows.Media.Colors.White;
                var black = System.Windows.Media.Colors.Black;
                var gray  = System.Windows.Media.Color.FromRgb(128, 128, 128);

                void SetB(string key, System.Windows.Media.Color color) 
                {
                    System.Windows.Application.Current.Resources[key] = new SolidColorBrush(color);
                    if (key.StartsWith("Brush_"))
                    {
                        System.Windows.Application.Current.Resources[key.Replace("Brush_", "Color_")] = color;
                    }
                    else
                    {
                        System.Windows.Application.Current.Resources[key + "Color"] = color;
                    }
                }

                System.Windows.Media.Color Mix(System.Windows.Media.Color c1, System.Windows.Media.Color c2, double amount)
                {
                    return System.Windows.Media.Color.FromRgb(
                        (byte)(c1.R * (1 - amount) + c2.R * amount),
                        (byte)(c1.G * (1 - amount) + c2.G * amount),
                        (byte)(c1.B * (1 - amount) + c2.B * amount));
                }

                // First old resources
                SetB("AccentBase", baseColor);
                SetB("AccentPrimary", Mix(baseColor, black, 0.15));
                SetB("AccentDark",    Mix(baseColor, black, 0.35));
                SetB("AccentPale",    Mix(baseColor, white, 0.5));
                SetB("AccentText",    Mix(baseColor, white, 0.75));
                SetB("AccentTextLight", Mix(baseColor, white, 0.85));
                SetB("AccentVeryLight", Mix(baseColor, white, 0.80));
                SetB("AccentMuted",   Mix(baseColor, gray, 0.6));
                SetB("AccentBgMain",    Mix(baseColor, black, 0.96)); 
                SetB("AccentBgDarker",  Mix(baseColor, black, 0.98)); 
                SetB("AccentBgBorder1", Mix(baseColor, black, 0.93)); 
                SetB("AccentBgInput",   Mix(baseColor, black, 0.95)); 
                SetB("AccentBgBorder2", Mix(baseColor, black, 0.94)); 
                SetB("AccentBgSystem",  Mix(baseColor, black, 0.97)); 
                SetB("AccentUserBubble",Mix(baseColor, black, 0.50)); 
                SetB("AccentUserShadow",Mix(baseColor, black, 0.70)); 
                SetB("AccentAsstBubble",Mix(baseColor, black, 0.95)); 
                SetB("AccentAsstBorder",Mix(baseColor, black, 0.90)); 
                SetB("AccentNavBg",     Mix(baseColor, black, 0.92)); 
                SetB("AccentListHovBg", Mix(baseColor, black, 0.94)); 
                SetB("AccentScrollThm", Mix(baseColor, black, 0.88)); 
                SetB("AccentAsstTime",  Mix(baseColor, black, 0.85)); 
                SetB("AccentCalSel",    Mix(baseColor, black, 0.90)); 
                SetB("AccentCalToday",  Mix(baseColor, black, 0.96)); 
                SetB("AccentCalBorder", Mix(baseColor, black, 0.85)); 
                SetB("AccentCalWnd",    Mix(baseColor, black, 0.80));
                System.Windows.Application.Current.Resources["AccentBaseColor"] = baseColor;

                // DYNAMIC HSL TRANSLATOR
                RgbToHsl(baseColor.R, baseColor.G, baseColor.B, out double newH, out double newS, out _);

                string[] hexesToTransform = new string[] {
                    "030B05", "142018", "2A4030", "163025", "071008", "2A4232", "0A1A0D", "2A0808", "FF5555",
                    "040C07", "08180C", "8AC8A0", "2A4832", "071209", "1A3020", "102818", "050D07", "060F08", 
                    "0F2818", "2A6040", "1A4A28", "4A8060", "5A8868", "B39DDB", "040B05", "1A4A2A", "0A1C0E", 
                    "1A4030", "A8D8B8", "1A0808", "3A1010", "CC3333", "FF6666", "3A2020", "2D4A3A", "1A3A4A", 
                    "3A1A1A", "2A3A1A", "1E1E3A", "B9F6CA", "1E3028", "6A9878", "0D1F14", "143020", "00E676",
                    "00C853", "009940", "80FFB8", "B8E8C8", "C8F0D8"
                };

                foreach (var hex in hexesToTransform)
                {
                    var origC = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF" + hex);
                    RgbToHsl(origC.R, origC.G, origC.B, out double origH, out double origS, out double origL);

                    // If it's pure red/pink/emotion error color, don't brutally tint it
                    System.Windows.Media.Color finalColor = origC;
                    if (origH > 0.15 && origH < 0.6) // Original Theme greens are in 0.3-0.5 range (151 deg)
                    {
                        double finalS = origS * (newS > 0 ? newS : 1);
                        if (finalS > 1) finalS = 1;
                        finalColor = HslToRgb(newH, finalS, origL);
                    }
                    SetB("Brush_" + hex, finalColor);
                }

                // Stable premium shell colors. Keep the app coherent even if legacy settings
                // still contain the old green matrix accent.
                var cyan = System.Windows.Media.Color.FromRgb(34, 211, 238);
                var violet = System.Windows.Media.Color.FromRgb(167, 139, 250);
                SetB("AccentBase",        cyan);
                SetB("AccentPrimary",     violet);
                SetB("AccentDark",        System.Windows.Media.Color.FromRgb(124, 58, 237));
                SetB("AccentPale",        System.Windows.Media.Color.FromRgb(196, 181, 253));
                SetB("AccentText",        System.Windows.Media.Color.FromRgb(201, 215, 234));
                SetB("AccentTextLight",   System.Windows.Media.Color.FromRgb(244, 248, 255));
                SetB("AccentVeryLight",   System.Windows.Media.Color.FromRgb(255, 255, 255));
                SetB("AccentMuted",       System.Windows.Media.Color.FromRgb(143, 163, 190));
                SetB("AccentDim",         System.Windows.Media.Color.FromRgb(88, 101, 122));
                SetB("AccentBgMain",      System.Windows.Media.Color.FromRgb(5, 7, 19));
                SetB("AccentBgBase",      System.Windows.Media.Color.FromRgb(8, 11, 22));
                SetB("AccentBgDarker",    System.Windows.Media.Color.FromRgb(6, 9, 22));
                SetB("AccentBgPanel",     System.Windows.Media.Color.FromRgb(12, 19, 36));
                SetB("AccentBgCard",      System.Windows.Media.Color.FromRgb(17, 26, 45));
                SetB("AccentBgHover",     System.Windows.Media.Color.FromRgb(23, 36, 58));
                SetB("AccentBgInput",     System.Windows.Media.Color.FromArgb(176, 8, 16, 32));
                SetB("AccentBgSystem",    System.Windows.Media.Color.FromArgb(153, 17, 26, 45));
                SetB("AccentBgBorder1",   System.Windows.Media.Color.FromArgb(36, 34, 211, 238));
                SetB("AccentBgBorder2",   System.Windows.Media.Color.FromArgb(48, 167, 139, 250));
                SetB("AccentAsstBorder",  System.Windows.Media.Color.FromArgb(102, 34, 211, 238));
                SetB("AccentAsstBubble",  System.Windows.Media.Color.FromArgb(176, 6, 20, 36));
                SetB("AccentAsstTime",    System.Windows.Media.Color.FromRgb(111, 130, 154));
                SetB("AccentUserBubble",  System.Windows.Media.Color.FromRgb(91, 46, 145));
                SetB("AccentUserShadow",  System.Windows.Media.Color.FromRgb(124, 58, 237));
                SetB("AccentNavBg",       System.Windows.Media.Color.FromArgb(34, 167, 139, 250));
                SetB("AccentScrollThm",   System.Windows.Media.Color.FromArgb(85, 34, 211, 238));
                SetB("AccentListHovBg",   System.Windows.Media.Color.FromArgb(18, 34, 211, 238));
                System.Windows.Application.Current.Resources["AccentBaseColor"] = cyan;
            }
            catch (Exception suppressedEx139) { KokoSystemLog.Write("THEMEMANAGER-CATCH", "ApplyTheme failed near source line 139: " + suppressedEx139); }
        }

        private static void RgbToHsl(byte r, byte g, byte b, out double h, out double s, out double l)
        {
            double dr = r / 255.0, dg = g / 255.0, db = b / 255.0;
            double max = Math.Max(dr, Math.Max(dg, db));
            double min = Math.Min(dr, Math.Min(dg, db));
            l = (max + min) / 2;
            if (max == min) { h = 0; s = 0; }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == dr) h = (dg - db) / d + (dg < db ? 6 : 0);
                else if (max == dg) h = (db - dr) / d + 2;
                else h = (dr - dg) / d + 4;
                h /= 6;
            }
        }

        private static System.Windows.Media.Color HslToRgb(double h, double s, double l)
        {
            if (s == 0) { byte v = (byte)(l * 255); return System.Windows.Media.Color.FromRgb(v, v, v); }
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            double r = HueToRgb(p, q, h + 1.0 / 3);
            double g = HueToRgb(p, q, h);
            double b = HueToRgb(p, q, h - 1.0 / 3);
            return System.Windows.Media.Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }
}
