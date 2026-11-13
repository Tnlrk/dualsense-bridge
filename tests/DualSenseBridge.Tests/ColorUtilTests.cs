using DualSenseBridge.UI;

namespace DualSenseBridge.Tests;

public class ColorUtilTests
{
    [Theory]
    [InlineData(0, 1, 1, 0xFF0000)]
    [InlineData(120, 1, 1, 0x00FF00)]
    [InlineData(240, 1, 1, 0x0000FF)]
    [InlineData(60, 1, 1, 0xFFFF00)]
    [InlineData(200, 0, 1, 0xFFFFFF)]
    [InlineData(360, 1, 1, 0xFF0000)]
    public void FromHsv(double h, double s, double v, int expected)
    {
        Assert.Equal(expected, ColorUtil.FromHsv(h, s, v));
    }

    [Theory]
    [InlineData(0x0040FF)]
    [InlineData(0xFF2D9A)]
    [InlineData(0x8A2BFF)]
    [InlineData(0x00E676)]
    public void RoundTrip(int rgb)
    {
        var (h, s, v) = ColorUtil.ToHsv(rgb);
        Assert.Equal(rgb, ColorUtil.FromHsv(h, s, v));
    }

    [Fact]
    public void White_HasNoSaturation()
    {
        var (_, s, v) = ColorUtil.ToHsv(0xFFFFFF);
        Assert.Equal(0, s);
        Assert.Equal(1, v);
    }
}
