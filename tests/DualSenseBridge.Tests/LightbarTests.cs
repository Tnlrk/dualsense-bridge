using DualSenseBridge.Bridge;

namespace DualSenseBridge.Tests;

public class LightbarTests
{
    [Fact]
    public void FullBrightness_KeepsColor()
    {
        var s = new AppSettings { LightbarColor = 0x0040FF, LightbarBrightness = 100 };
        Assert.Equal(0x0040FF, Lightbar.Compute(s, lowBattery: false));
    }

    [Fact]
    public void HalfBrightness_IsQuadratic()
    {
        // 50 % → 0,25 × hodnota kanálu.
        Assert.Equal(0x400040, Lightbar.Scale(0xFF00FF, 50));
    }

    [Fact]
    public void LowBrightness_KeepsHueVisible()
    {
        // Nenulové kanály nespadnou na 0, aby lišta nezhasla ani při 1 %.
        Assert.Equal(0x010001, Lightbar.Scale(0xFF00FF, 1));
    }

    [Fact]
    public void Disabled_IsOff()
    {
        var s = new AppSettings { LightbarEnabled = false };
        Assert.Equal(0, Lightbar.Compute(s, lowBattery: false));
    }

    [Fact]
    public void LowBattery_ShowsRedEvenWhenDisabled()
    {
        var s = new AppSettings { LightbarEnabled = false, LowBatteryLightbar = true };
        int rgb = Lightbar.Compute(s, lowBattery: true);
        Assert.True(rgb >> 16 > 0);
        Assert.Equal(0, rgb & 0xFFFF);
    }

    [Fact]
    public void LowBatteryWarningOff_RespectsDisabled()
    {
        var s = new AppSettings { LightbarEnabled = false, LowBatteryLightbar = false };
        Assert.Equal(0, Lightbar.Compute(s, lowBattery: true));
    }
}
