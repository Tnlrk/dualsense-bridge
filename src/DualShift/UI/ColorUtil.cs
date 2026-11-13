namespace DualShift.UI;

/// <summary>Převody RGB ↔ HSV pro výběr barvy světelné lišty (odstín + sytost; jas je zvlášť).</summary>
public static class ColorUtil
{
    /// <param name="hue">0–360</param>
    /// <param name="saturation">0–1</param>
    /// <param name="value">0–1</param>
    public static int FromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        double c = value * saturation;
        double x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        double m = value - c;
        var (r, g, b) = (int)(hue / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        int R = (int)Math.Round((r + m) * 255), G = (int)Math.Round((g + m) * 255), B = (int)Math.Round((b + m) * 255);
        return (R << 16) | (G << 8) | B;
    }

    public static (double Hue, double Saturation, double Value) ToHsv(int rgb)
    {
        double r = ((rgb >> 16) & 0xFF) / 255.0, g = ((rgb >> 8) & 0xFF) / 255.0, b = (rgb & 0xFF) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
        }
        if (h < 0) h += 360;
        return (h, max == 0 ? 0 : d / max, max);
    }
}
