using System.Text;
using DualShift.DualSense;

namespace DualShift.Tests;

public class OutputReportBuilderTests
{
    private static readonly OutputState Sample = new()
    {
        MotorLeft = 200,
        MotorRight = 50,
        LightbarR = 1, LightbarG = 2, LightbarB = 3,
        PlayerLeds = 0x04,
        RightTrigger = new TriggerEffect { Kind = TriggerEffectKind.Resistance, StartPosition = 0, Strength = 8 },
    };

    [Fact]
    public void Crc32_KnownVector()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void Crc32_WithSeedEqualsPrefixedData()
    {
        var data = new byte[] { 1, 2, 3, 4 };
        Assert.Equal(Crc32.Compute(new byte[] { 0xA2, 1, 2, 3, 4 }), Crc32.Compute(0xA2, data));
    }

    [Fact]
    public void Usb_Layout()
    {
        var r = OutputReportBuilder.Build(Sample, ConnectionType.Usb, 0, vibrationV2: false);
        Assert.Equal(48, r.Length);
        Assert.Equal(0x02, r[0]);
        Assert.Equal(0x01 | 0x02 | 0x04 | 0x08, r[1]);   // vibrace + oba triggery
        Assert.Equal(0x04 | 0x10, r[2]);                 // lišta + LED hráče
        Assert.Equal(50, r[3]);                          // pravý (malý) motor
        Assert.Equal(200, r[4]);                         // levý (velký) motor
        Assert.Equal(0x21, r[11]);                       // R2 efekt odpor
        Assert.Equal(0x05, r[22]);                       // L2 vypnuto
        Assert.Equal(0x00, r[39]);                       // valid_flag2
        Assert.Equal(0x04, r[44]);                       // LED hráče
        Assert.Equal(new byte[] { 1, 2, 3 }, r[45..48]); // RGB
    }

    [Fact]
    public void Usb_VibrationV2_UsesFlag2()
    {
        var r = OutputReportBuilder.Build(Sample, ConnectionType.Usb, 0, vibrationV2: true, releaseLightbar: true);
        Assert.Equal(0, r[1] & 0x01);
        Assert.Equal(0x04 | 0x02, r[39]);
        Assert.Equal(0x02, r[42]);
    }

    [Fact]
    public void Bluetooth_LayoutAndCrc()
    {
        var r = OutputReportBuilder.Build(Sample, ConnectionType.Bluetooth, 3, vibrationV2: false);
        Assert.Equal(78, r.Length);
        Assert.Equal(0x31, r[0]);
        Assert.Equal(0x30, r[1]);
        Assert.Equal(0x10, r[2]);
        Assert.Equal(50, r[5]);
        Assert.Equal(200, r[6]);
        Assert.Equal(0x21, r[13]);
        Assert.Equal(new byte[] { 1, 2, 3 }, r[47..50]);

        uint crc = Crc32.Compute(0xA2, r.AsSpan(0, 74));
        Assert.Equal(crc, BitConverter.ToUInt32(r, 74));
    }

    [Fact]
    public void Trigger_ResistanceFullRange()
    {
        var p = new byte[11];
        new TriggerEffect { Kind = TriggerEffectKind.Resistance, StartPosition = 0, Strength = 8 }.WriteTo(p);
        Assert.Equal(new byte[] { 0x21, 0xFF, 0x03, 0xFF, 0xFF, 0xFF, 0x3F, 0, 0, 0, 0 }, p);
    }

    [Fact]
    public void Trigger_Weapon()
    {
        var p = new byte[11];
        new TriggerEffect { Kind = TriggerEffectKind.Weapon, StartPosition = 2, EndPosition = 5, Strength = 5 }.WriteTo(p);
        Assert.Equal(new byte[] { 0x25, 0x24, 0x00, 0x04, 0, 0, 0, 0, 0, 0, 0 }, p);
    }

    [Fact]
    public void Trigger_Vibration()
    {
        var p = new byte[11];
        TriggerEffect.VibrationOf(strength: 1, frequency: 40, position: 9).WriteTo(p);
        Assert.Equal(new byte[] { 0x26, 0x00, 0x02, 0, 0, 0, 0, 0, 0, 40, 0 }, p);
    }

    [Fact]
    public void Trigger_ZeroStrengthIsOff()
    {
        var p = new byte[11];
        new TriggerEffect { Kind = TriggerEffectKind.Resistance, Strength = 0 }.WriteTo(p);
        Assert.Equal(0x05, p[0]);
        Assert.All(p[1..], b => Assert.Equal(0, b));
    }
}
