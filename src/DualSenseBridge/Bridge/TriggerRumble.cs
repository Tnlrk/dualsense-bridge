using DualSenseBridge.DualSense;

namespace DualSenseBridge.Bridge;

/// <summary>
/// Náhrada impulsních triggerů: Xbox 360 ovladač vibrace triggerů nedostává, proto se odvozují
/// z běžných vibrací hry. Volitelně vibruje jen trigger, který je právě zmáčknutý
/// (brzda → L2, plyn → R2).
/// </summary>
public static class TriggerRumble
{
    /// <summary>Od kolika je trigger „zmáčknutý“ (~10 %).</summary>
    public const byte PressThreshold = 25;
    /// <summary>Pod kolik musí klesnout, aby se bral jako puštěný (hystereze proti blikání).</summary>
    public const byte ReleaseThreshold = 15;
    /// <summary>Slabší vibrace hry se ignorují.</summary>
    public const int MinRumble = 10;
    public const int Frequency = 35;

    public static bool IsPressed(byte value, bool wasPressed) =>
        wasPressed ? value > ReleaseThreshold : value >= PressThreshold;

    /// <summary>Výsledné efekty triggerů pro danou sílu vibrací hry (0–255).</summary>
    public static (TriggerEffect Left, TriggerEffect Right) Compose(
        AppSettings s, int rumble, bool l2Pressed, bool r2Pressed)
    {
        var left = s.LeftTrigger;
        var right = s.RightTrigger;
        if (!s.TriggerRumble || rumble <= MinRumble) return (left, right);

        int strength = 1 + Math.Clamp(rumble, 0, 255) * 7 / 255;
        var vib = TriggerEffect.VibrationOf(strength, Frequency);
        if (!s.TriggerRumbleOnlyPressed || l2Pressed) left = vib;
        if (!s.TriggerRumbleOnlyPressed || r2Pressed) right = vib;
        return (left, right);
    }
}
