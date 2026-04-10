using Bam.Generators.Ux.Models;

namespace Bam.Generators.Ux
{
    public static class ColorPaletteGenerator
    {
        private static readonly Dictionary<string, UxThemeModel> IndustryDefaults = new()
        {
            ["healthcare"] = new()
            {
                PrimaryColor = "#0891B2", SecondaryColor = "#164E63", AccentColor = "#06B6D4",
                BackgroundColor = "#F8FAFC", TextColor = "#0F172A", HeadingColor = "#164E63"
            },
            ["retail"] = new()
            {
                PrimaryColor = "#7C3AED", SecondaryColor = "#4C1D95", AccentColor = "#A78BFA",
                BackgroundColor = "#F8FAFC", TextColor = "#0F172A", HeadingColor = "#4C1D95"
            },
            ["construction"] = new()
            {
                PrimaryColor = "#D97706", SecondaryColor = "#92400E", AccentColor = "#F59E0B",
                BackgroundColor = "#FFFBEB", TextColor = "#0F172A", HeadingColor = "#92400E"
            },
            ["legal"] = new()
            {
                PrimaryColor = "#1E3A5F", SecondaryColor = "#0F172A", AccentColor = "#3B82F6",
                BackgroundColor = "#F8FAFC", TextColor = "#1E293B", HeadingColor = "#0F172A"
            },
            ["energy"] = new()
            {
                PrimaryColor = "#15803D", SecondaryColor = "#14532D", AccentColor = "#22C55E",
                BackgroundColor = "#F0FDF4", TextColor = "#0F172A", HeadingColor = "#14532D"
            },
            ["manufacturing"] = new()
            {
                PrimaryColor = "#475569", SecondaryColor = "#1E293B", AccentColor = "#64748B",
                BackgroundColor = "#F8FAFC", TextColor = "#0F172A", HeadingColor = "#1E293B"
            },
        };

        public static UxThemeModel Suggest(string companyName, string industry)
        {
            string key = industry.ToLowerInvariant();
            UxThemeModel baseTheme = IndustryDefaults.GetValueOrDefault(key) ?? IndustryDefaults["legal"];

            int hash = GetStableHash(companyName);
            int hueShift = (hash % 30) - 15; // -15 to +15 degree hue shift

            return new UxThemeModel
            {
                PrimaryColor = ShiftHue(baseTheme.PrimaryColor, hueShift),
                SecondaryColor = ShiftHue(baseTheme.SecondaryColor, hueShift),
                AccentColor = ShiftHue(baseTheme.AccentColor, hueShift),
                BackgroundColor = baseTheme.BackgroundColor,
                TextColor = baseTheme.TextColor,
                HeadingColor = ShiftHue(baseTheme.HeadingColor, hueShift),
                FontFamily = baseTheme.FontFamily,
                HeadingFontFamily = baseTheme.HeadingFontFamily
            };
        }

        private static int GetStableHash(string value)
        {
            int hash = 5381;
            foreach (char c in value)
            {
                hash = ((hash << 5) + hash) + c;
            }
            return Math.Abs(hash);
        }

        private static string ShiftHue(string hex, int degrees)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 7) return hex;

            int r = Convert.ToInt32(hex.Substring(1, 2), 16);
            int g = Convert.ToInt32(hex.Substring(3, 2), 16);
            int b = Convert.ToInt32(hex.Substring(5, 2), 16);

            RgbToHsl(r, g, b, out double h, out double s, out double l);
            h = (h + degrees + 360) % 360;
            HslToRgb(h, s, l, out int nr, out int ng, out int nb);

            return $"#{nr:X2}{ng:X2}{nb:X2}";
        }

        private static void RgbToHsl(int r, int g, int b, out double h, out double s, out double l)
        {
            double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double diff = max - min;

            l = (max + min) / 2.0;

            if (Math.Abs(diff) < 0.00001)
            {
                h = 0; s = 0;
                return;
            }

            s = l > 0.5 ? diff / (2.0 - max - min) : diff / (max + min);

            if (Math.Abs(max - rd) < 0.00001) h = ((gd - bd) / diff + (gd < bd ? 6 : 0)) * 60;
            else if (Math.Abs(max - gd) < 0.00001) h = ((bd - rd) / diff + 2) * 60;
            else h = ((rd - gd) / diff + 4) * 60;
        }

        private static void HslToRgb(double h, double s, double l, out int r, out int g, out int b)
        {
            if (Math.Abs(s) < 0.00001)
            {
                r = g = b = (int)Math.Round(l * 255);
                return;
            }

            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            double hNorm = h / 360.0;

            r = (int)Math.Round(HueToRgb(p, q, hNorm + 1.0 / 3) * 255);
            g = (int)Math.Round(HueToRgb(p, q, hNorm) * 255);
            b = (int)Math.Round(HueToRgb(p, q, hNorm - 1.0 / 3) * 255);
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
