namespace DualSenseBridge.DualSense;

public enum TriggerEffectKind
{
    /// <summary>Normální měkký trigger.</summary>
    Off,
    /// <summary>Odpor od zvolené pozice.</summary>
    Resistance,
    /// <summary>Odpor mezi start a end pozicí, pak „cvaknutí“ jako u spouště.</summary>
    Weapon,
    /// <summary>Trigger vibruje od zvolené pozice.</summary>
    Vibration,
}

/// <summary>
/// Nastavení adaptivního triggeru. Pozice 0–9 (0 = úplně nahoře), síla 1–8, frekvence 1–255 Hz.
/// Kódování 11 bajtů parametrů podle veřejně zdokumentovaného formátu DualSense.
/// </summary>
public sealed record TriggerEffect
{
    public TriggerEffectKind Kind { get; init; } = TriggerEffectKind.Off;
    public int StartPosition { get; init; } = 3;
    public int EndPosition { get; init; } = 6;
    public int Strength { get; init; } = 5;
    public int Frequency { get; init; } = 30;

    public const int ParamLength = 11;

    private const byte ModeOff = 0x05;
    private const byte ModeFeedback = 0x21;
    private const byte ModeWeapon = 0x25;
    private const byte ModeVibration = 0x26;

    public static readonly TriggerEffect None = new();

    public static TriggerEffect VibrationOf(int strength, int frequency, int position = 0) =>
        new() { Kind = TriggerEffectKind.Vibration, Strength = strength, Frequency = frequency, StartPosition = position };

    /// <summary>Zapíše 11 bajtů parametrů efektu do <paramref name="dest"/>.</summary>
    public void WriteTo(Span<byte> dest)
    {
        if (dest.Length < ParamLength) throw new ArgumentException("Buffer too small", nameof(dest));
        dest[..ParamLength].Clear();

        int strength = Math.Clamp(Strength, 0, 8);
        switch (Kind)
        {
            case TriggerEffectKind.Resistance when strength > 0:
            {
                int pos = Math.Clamp(StartPosition, 0, 9);
                var (active, force) = Zones(pos, strength);
                dest[0] = ModeFeedback;
                WriteU16(dest[1..], active);
                WriteU32(dest[3..], force);
                return;
            }
            case TriggerEffectKind.Weapon when strength > 0:
            {
                int start = Math.Clamp(StartPosition, 2, 7);
                int end = Math.Clamp(EndPosition, start + 1, 8);
                dest[0] = ModeWeapon;
                WriteU16(dest[1..], (ushort)((1 << start) | (1 << end)));
                dest[3] = (byte)(strength - 1);
                return;
            }
            case TriggerEffectKind.Vibration when strength > 0 && Frequency > 0:
            {
                int pos = Math.Clamp(StartPosition, 0, 9);
                var (active, amp) = Zones(pos, strength);
                dest[0] = ModeVibration;
                WriteU16(dest[1..], active);
                WriteU32(dest[3..], amp);
                dest[9] = (byte)Math.Clamp(Frequency, 1, 255);
                return;
            }
            default:
                dest[0] = ModeOff;
                return;
        }
    }

    private static (ushort Active, uint Values) Zones(int fromPosition, int strength)
    {
        uint value = (uint)((strength - 1) & 0x07);
        uint values = 0;
        ushort active = 0;
        for (int i = fromPosition; i < 10; i++)
        {
            values |= value << (3 * i);
            active |= (ushort)(1 << i);
        }
        return (active, values);
    }

    private static void WriteU16(Span<byte> d, ushort v)
    {
        d[0] = (byte)v;
        d[1] = (byte)(v >> 8);
    }

    private static void WriteU32(Span<byte> d, uint v)
    {
        d[0] = (byte)v;
        d[1] = (byte)(v >> 8);
        d[2] = (byte)(v >> 16);
        d[3] = (byte)(v >> 24);
    }
}
