namespace DualShift.DualSense;

/// <summary>
/// Převod surových HID input reportů DualSense na <see cref="DualSenseState"/>.
/// Rozložení dat podle linuxového ovladače hid-playstation.
/// </summary>
public static class InputReportParser
{
    public const byte UsbReportId = 0x01;
    public const byte BtFullReportId = 0x31;
    public const byte BtSimpleReportId = 0x01;

    // Offsety uvnitř společné struktury input reportu (bez report ID).
    private const int OffLX = 0, OffLY = 1, OffRX = 2, OffRY = 3, OffL2 = 4, OffR2 = 5;
    private const int OffButtons = 7;
    private const int OffStatus = 52;

    /// <summary>
    /// Zparsuje report. Vrací false, pokud report není podporovaný (např. jiné report ID).
    /// <paramref name="isFullReport"/> = false znamená zkrácený BT report bez baterie
    /// (ovladač ještě nebyl přepnut do plného režimu).
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> report, ConnectionType connection,
        out DualSenseState state, out bool isFullReport)
    {
        state = default;
        isFullReport = false;
        if (report.Length < 10) return false;

        int b; // index začátku společné struktury
        if (connection == ConnectionType.Usb && report[0] == UsbReportId)
        {
            b = 1;
        }
        else if (connection == ConnectionType.Bluetooth && report[0] == BtFullReportId)
        {
            b = 2;
        }
        else if (connection == ConnectionType.Bluetooth && report[0] == BtSimpleReportId)
        {
            // Zkrácený report: LX LY RX RY B0 B1 B2 L2 R2
            state = new DualSenseState(report[1], report[2], report[3], report[4], report[8], report[9],
                ParseButtons(report[5], report[6], report[7]), null, ChargingState.Unknown);
            return true;
        }
        else
        {
            return false;
        }

        if (report.Length < b + OffStatus + 1) return false;

        var buttons = ParseButtons(report[b + OffButtons], report[b + OffButtons + 1], report[b + OffButtons + 2]);
        var (pct, charging) = ParseBattery(report[b + OffStatus]);
        state = new DualSenseState(
            report[b + OffLX], report[b + OffLY], report[b + OffRX], report[b + OffRY],
            report[b + OffL2], report[b + OffR2], buttons, pct, charging);
        isFullReport = true;
        return true;
    }

    internal static DsButtons ParseButtons(byte b0, byte b1, byte b2)
    {
        var r = DsButtons.None;

        r |= (b0 & 0x0F) switch
        {
            0 => DsButtons.DpadUp,
            1 => DsButtons.DpadUp | DsButtons.DpadRight,
            2 => DsButtons.DpadRight,
            3 => DsButtons.DpadDown | DsButtons.DpadRight,
            4 => DsButtons.DpadDown,
            5 => DsButtons.DpadDown | DsButtons.DpadLeft,
            6 => DsButtons.DpadLeft,
            7 => DsButtons.DpadUp | DsButtons.DpadLeft,
            _ => DsButtons.None,
        };
        if ((b0 & 0x10) != 0) r |= DsButtons.Square;
        if ((b0 & 0x20) != 0) r |= DsButtons.Cross;
        if ((b0 & 0x40) != 0) r |= DsButtons.Circle;
        if ((b0 & 0x80) != 0) r |= DsButtons.Triangle;

        if ((b1 & 0x01) != 0) r |= DsButtons.L1;
        if ((b1 & 0x02) != 0) r |= DsButtons.R1;
        if ((b1 & 0x04) != 0) r |= DsButtons.L2;
        if ((b1 & 0x08) != 0) r |= DsButtons.R2;
        if ((b1 & 0x10) != 0) r |= DsButtons.Create;
        if ((b1 & 0x20) != 0) r |= DsButtons.Options;
        if ((b1 & 0x40) != 0) r |= DsButtons.L3;
        if ((b1 & 0x80) != 0) r |= DsButtons.R3;

        if ((b2 & 0x01) != 0) r |= DsButtons.PS;
        if ((b2 & 0x02) != 0) r |= DsButtons.Touchpad;
        if ((b2 & 0x04) != 0) r |= DsButtons.Mute;
        return r;
    }

    internal static (int? Percent, ChargingState State) ParseBattery(byte status)
    {
        int level = status & 0x0F;
        int cs = status >> 4;
        int pct = Math.Min(level * 10 + 5, 100);
        return cs switch
        {
            0x0 => (pct, ChargingState.Discharging),
            0x1 => (pct, ChargingState.Charging),
            0x2 => (100, ChargingState.Full),
            0xA or 0xB or 0xF => (null, ChargingState.Error),
            _ => (null, ChargingState.Unknown),
        };
    }
}
