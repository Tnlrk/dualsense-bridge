using DualSenseBridge.DualSense;

namespace DualSenseBridge.Bridge;

/// <summary>Stav virtuálního Xbox 360 ovladače (formát XInput).</summary>
public readonly record struct XInputState(
    ushort Buttons, byte LeftTrigger, byte RightTrigger,
    short LeftX, short LeftY, short RightX, short RightY);

/// <summary>Čisté mapování DualSense → Xbox 360.</summary>
public static class XInputMapper
{
    // Bitová maska tlačítek XInput (XINPUT_GAMEPAD_*).
    public const ushort DpadUp = 0x0001, DpadDown = 0x0002, DpadLeft = 0x0004, DpadRight = 0x0008;
    public const ushort Start = 0x0010, Back = 0x0020, LeftThumb = 0x0040, RightThumb = 0x0080;
    public const ushort LeftShoulder = 0x0100, RightShoulder = 0x0200, Guide = 0x0400;
    public const ushort A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;

    private static readonly (DsButtons Ds, ushort Xi)[] ButtonMap =
    {
        (DsButtons.Cross, A),
        (DsButtons.Circle, B),
        (DsButtons.Square, X),
        (DsButtons.Triangle, Y),
        (DsButtons.L1, LeftShoulder),
        (DsButtons.R1, RightShoulder),
        (DsButtons.L3, LeftThumb),
        (DsButtons.R3, RightThumb),
        (DsButtons.Create, Back),
        (DsButtons.Options, Start),
        (DsButtons.PS, Guide),
        (DsButtons.DpadUp, DpadUp),
        (DsButtons.DpadDown, DpadDown),
        (DsButtons.DpadLeft, DpadLeft),
        (DsButtons.DpadRight, DpadRight),
    };

    /// <summary>Název Xbox tlačítka, na které je dané tlačítko DualSense namapované (null = nic).</summary>
    public static string? XboxLabel(DsButtons button, bool touchpadAsBack)
    {
        if (button == DsButtons.Touchpad) return touchpadAsBack ? "Back" : null;
        if (button == DsButtons.L2) return "LT";
        if (button == DsButtons.R2) return "RT";
        foreach (var (ds, xi) in ButtonMap)
            if (ds == button) return Name(xi);
        return null;
    }

    private static string Name(ushort xi) => xi switch
    {
        A => "A", B => "B", X => "X", Y => "Y",
        LeftShoulder => "LB", RightShoulder => "RB",
        LeftThumb => "LS", RightThumb => "RS",
        Back => "Back", Start => "Start", Guide => "Guide",
        DpadUp => "D-pad ↑", DpadDown => "D-pad ↓", DpadLeft => "D-pad ←", DpadRight => "D-pad →",
        _ => "?",
    };

    public static XInputState Map(in DualSenseState s, bool touchpadAsBack)
    {
        ushort buttons = 0;
        foreach (var (ds, xi) in ButtonMap)
            if ((s.Buttons & ds) != 0) buttons |= xi;
        if (touchpadAsBack && (s.Buttons & DsButtons.Touchpad) != 0) buttons |= Back;

        return new XInputState(
            buttons, s.L2, s.R2,
            Axis(s.LeftX), InvertedAxis(s.LeftY),
            Axis(s.RightX), InvertedAxis(s.RightY));
    }

    /// <summary>0–255 → -32768…32767 (128 ≈ střed).</summary>
    public static short Axis(byte v) => (short)(v * 257 - 32768);

    /// <summary>DualSense má osu Y dolů kladnou, XInput nahoru kladnou.</summary>
    public static short InvertedAxis(byte v) => (short)(32767 - v * 257);
}
