namespace DualSenseBridge.Bridge;

/// <summary>Výsledná barva světelné lišty podle nastavení (zapnutí, jas, varování slabé baterie).</summary>
public static class Lightbar
{
    public const int LowBatteryRgb = 0xFF0000;
    /// <summary>Varování slabé baterie je vidět, i když je lišta ztlumená nebo vypnutá.</summary>
    public const int MinWarningBrightness = 40;

    public static int Compute(AppSettings s, bool lowBattery)
    {
        if (s.LowBatteryLightbar && lowBattery)
        {
            int b = s.LightbarEnabled ? Math.Max(s.LightbarBrightness, MinWarningBrightness) : MinWarningBrightness;
            return Scale(LowBatteryRgb, b);
        }
        return s.LightbarEnabled ? Scale(s.LightbarColor, s.LightbarBrightness) : 0;
    }

    /// <summary>
    /// Ztlumení barvy na daný jas (0–100 %). Kvadratická křivka, aby 50 % i na oko vypadalo jako poloviční jas
    /// (LED se lidskému oku jeví jasnější, než odpovídá lineární hodnotě).
    /// </summary>
    public static int Scale(int rgb, int percent)
    {
        double f = Math.Pow(Math.Clamp(percent, 0, 100) / 100.0, 2);
        int Ch(int shift)
        {
            int c = (rgb >> shift) & 0xFF;
            if (c == 0 || f == 0) return 0;
            return Math.Max(1, (int)Math.Round(c * f));
        }
        return (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
    }
}
