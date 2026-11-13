namespace DualShift.DualSense;

/// <summary>Požadovaný stav výstupu ovladače (vibrace, triggery, LED).</summary>
public sealed record OutputState
{
    public byte MotorLeft { get; init; }   // velký (silný) motor
    public byte MotorRight { get; init; }  // malý (jemný) motor
    public TriggerEffect LeftTrigger { get; init; } = TriggerEffect.None;
    public TriggerEffect RightTrigger { get; init; } = TriggerEffect.None;
    public byte LightbarR { get; init; }
    public byte LightbarG { get; init; } = 0x40;
    public byte LightbarB { get; init; } = 0xFF;
    /// <summary>5 bitů – LEDky hráče pod touchpadem.</summary>
    public byte PlayerLeds { get; init; }
}

/// <summary>
/// Sestavení výstupních reportů. USB: report 0x02 (48 B). Bluetooth: report 0x31 (78 B) s CRC32.
/// </summary>
public static class OutputReportBuilder
{
    public const int UsbLength = 48;
    public const int BtLength = 78;

    // Offsety uvnitř společné části (47 B).
    private const int ValidFlag0 = 0, ValidFlag1 = 1, MotorRightOff = 2, MotorLeftOff = 3;
    private const int RightTriggerOff = 10, LeftTriggerOff = 21;
    private const int ValidFlag2 = 38, LightbarSetup = 41, PlayerLedsOff = 43, LightbarOff = 44;

    private const byte Flag0CompatibleVibration = 0x01;
    private const byte Flag0HapticsSelect = 0x02;
    private const byte Flag0RightTrigger = 0x04;
    private const byte Flag0LeftTrigger = 0x08;
    private const byte Flag1LightbarControl = 0x04;
    private const byte Flag1PlayerIndicator = 0x10;
    private const byte Flag2LightbarSetup = 0x02;
    private const byte Flag2CompatibleVibration2 = 0x04;
    private const byte LightbarSetupLightOut = 0x02;

    /// <param name="vibrationV2">Novější firmware používá jiný příznak pro vibrace; null = nevíme, pošli oba.</param>
    /// <param name="releaseLightbar">Ukončit úvodní modrou animaci lišty (stačí poprvé).</param>
    public static byte[] Build(OutputState s, ConnectionType connection, byte sequence,
        bool? vibrationV2 = null, bool releaseLightbar = false)
    {
        byte[] buf;
        int c; // začátek společné části
        if (connection == ConnectionType.Usb)
        {
            buf = new byte[UsbLength];
            buf[0] = 0x02;
            c = 1;
        }
        else
        {
            buf = new byte[BtLength];
            buf[0] = 0x31;
            buf[1] = (byte)((sequence & 0x0F) << 4);
            buf[2] = 0x10; // DS_OUTPUT_TAG
            c = 3;
        }

        byte f0 = Flag0HapticsSelect | Flag0RightTrigger | Flag0LeftTrigger;
        byte f2 = 0;
        if (vibrationV2 != true) f0 |= Flag0CompatibleVibration;
        if (vibrationV2 != false) f2 |= Flag2CompatibleVibration2;

        buf[c + ValidFlag0] = f0;
        buf[c + ValidFlag1] = Flag1LightbarControl | Flag1PlayerIndicator;
        buf[c + MotorRightOff] = s.MotorRight;
        buf[c + MotorLeftOff] = s.MotorLeft;
        s.RightTrigger.WriteTo(buf.AsSpan(c + RightTriggerOff, TriggerEffect.ParamLength));
        s.LeftTrigger.WriteTo(buf.AsSpan(c + LeftTriggerOff, TriggerEffect.ParamLength));

        if (releaseLightbar)
        {
            f2 |= Flag2LightbarSetup;
            buf[c + LightbarSetup] = LightbarSetupLightOut;
        }
        buf[c + ValidFlag2] = f2;
        buf[c + PlayerLedsOff] = (byte)(s.PlayerLeds & 0x1F);
        buf[c + LightbarOff] = s.LightbarR;
        buf[c + LightbarOff + 1] = s.LightbarG;
        buf[c + LightbarOff + 2] = s.LightbarB;

        if (connection == ConnectionType.Bluetooth)
        {
            uint crc = Crc32.Compute(0xA2, buf.AsSpan(0, BtLength - 4));
            buf[74] = (byte)crc;
            buf[75] = (byte)(crc >> 8);
            buf[76] = (byte)(crc >> 16);
            buf[77] = (byte)(crc >> 24);
        }
        return buf;
    }

    /// <summary>Rozsvícení LED hráče podle čísla (0–3) jako u PS5.</summary>
    public static byte PlayerLedsFor(int playerIndex) => playerIndex switch
    {
        0 => 0x04,
        1 => 0x0A,
        2 => 0x15,
        3 => 0x1B,
        _ => 0x00,
    };
}

/// <summary>Standardní CRC-32 (IEEE 802.3), jak ho DualSense vyžaduje u BT reportů.</summary>
public static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    private static uint[] CreateTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Compute(ReadOnlySpan<byte> data) => Finish(Update(0xFFFFFFFFu, data));

    /// <summary>CRC s jedním bajtem „seed“ na začátku (0xA2 pro výstup, 0xA1 pro vstup, 0xA3 pro feature).</summary>
    public static uint Compute(byte seed, ReadOnlySpan<byte> data)
    {
        uint crc = Update(0xFFFFFFFFu, stackalloc byte[] { seed });
        return Finish(Update(crc, data));
    }

    private static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint Finish(uint crc) => ~crc;
}
