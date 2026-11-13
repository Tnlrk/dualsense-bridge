using DualShift.Bridge;
using DualShift.DualSense;

namespace DualShift.Tests;

public class TriggerRumbleTests
{
    private static readonly TriggerEffect Resistance =
        new() { Kind = TriggerEffectKind.Resistance, StartPosition = 2, Strength = 4 };

    private static AppSettings Settings(bool onlyPressed = true) => new()
    {
        TriggerRumble = true,
        TriggerRumbleOnlyPressed = onlyPressed,
        LeftTrigger = Resistance,
        RightTrigger = TriggerEffect.None,
    };

    [Fact]
    public void OnlyPressedTriggerVibrates()
    {
        // Brzdění: L2 zmáčknutý, R2 puštěný.
        var (l, r) = TriggerRumble.Compose(Settings(), rumble: 255, l2Pressed: true, r2Pressed: false);
        Assert.Equal(TriggerEffectKind.Vibration, l.Kind);
        Assert.Equal(8, l.Strength);
        Assert.Equal(TriggerEffectKind.Off, r.Kind);
    }

    [Fact]
    public void ReleasedTriggerKeepsUserEffect()
    {
        var (l, r) = TriggerRumble.Compose(Settings(), rumble: 200, l2Pressed: false, r2Pressed: true);
        Assert.Equal(Resistance, l);
        Assert.Equal(TriggerEffectKind.Vibration, r.Kind);
    }

    [Fact]
    public void BothVibrateWhenOnlyPressedDisabled()
    {
        var (l, r) = TriggerRumble.Compose(Settings(onlyPressed: false), rumble: 128, l2Pressed: false, r2Pressed: false);
        Assert.Equal(TriggerEffectKind.Vibration, l.Kind);
        Assert.Equal(TriggerEffectKind.Vibration, r.Kind);
    }

    [Fact]
    public void WeakRumbleIgnored()
    {
        var (l, r) = TriggerRumble.Compose(Settings(), rumble: 5, l2Pressed: true, r2Pressed: true);
        Assert.Equal(Resistance, l);
        Assert.Equal(TriggerEffectKind.Off, r.Kind);
    }

    [Fact]
    public void DisabledFeatureKeepsUserEffects()
    {
        var s = Settings();
        s.TriggerRumble = false;
        var (l, _) = TriggerRumble.Compose(s, rumble: 255, l2Pressed: true, r2Pressed: true);
        Assert.Equal(Resistance, l);
    }

    [Theory]
    [InlineData(24, false, false)]
    [InlineData(25, false, true)]
    [InlineData(20, true, true)]   // hystereze: drží se zmáčknutý
    [InlineData(15, true, false)]
    public void PressHysteresis(byte value, bool was, bool expected)
    {
        Assert.Equal(expected, TriggerRumble.IsPressed(value, was));
    }
}
