using DualShift.DualSense;

namespace DualShift.Tests;

public class InputReportParserTests
{
    private static byte[] UsbReport(Action<byte[], int> fill)
    {
        var r = new byte[64];
        r[0] = 0x01;
        r[1] = r[2] = r[3] = r[4] = 0x80;
        r[8] = 0x08; // D-pad nic
        fill(r, 1);
        return r;
    }

    private static byte[] BtFullReport(Action<byte[], int> fill)
    {
        var r = new byte[78];
        r[0] = 0x31;
        r[2] = r[3] = r[4] = r[5] = 0x80;
        r[9] = 0x08;
        fill(r, 2);
        BitConverter.TryWriteBytes(r.AsSpan(74), Crc32.Compute(0xA1, r.AsSpan(0, 74)));
        return r;
    }

    [Fact]
    public void Usb_ParsesSticksTriggersButtonsAndBattery()
    {
        var r = UsbReport((r, b) =>
        {
            r[b + 0] = 10; r[b + 1] = 20; r[b + 2] = 200; r[b + 3] = 255;
            r[b + 4] = 128; r[b + 5] = 255;
            r[b + 7] = 0x20 | 0x02;   // Cross + D-pad doprava
            r[b + 8] = 0x01 | 0x20;   // L1 + Options
            r[b + 9] = 0x01;          // PS
            r[b + 52] = 0x17;         // nabíjí se, úroveň 7
        });

        Assert.True(InputReportParser.TryParse(r, ConnectionType.Usb, out var s, out var full));
        Assert.True(full);
        Assert.Equal((byte)10, s.LeftX);
        Assert.Equal((byte)20, s.LeftY);
        Assert.Equal((byte)200, s.RightX);
        Assert.Equal((byte)255, s.RightY);
        Assert.Equal((byte)128, s.L2);
        Assert.Equal((byte)255, s.R2);
        Assert.Equal(DsButtons.Cross | DsButtons.DpadRight | DsButtons.L1 | DsButtons.Options | DsButtons.PS, s.Buttons);
        Assert.Equal(75, s.BatteryPercent);
        Assert.Equal(ChargingState.Charging, s.Charging);
    }

    [Fact]
    public void Usb_BatteryOffsetIs53()
    {
        var r = UsbReport((r, _) => r[53] = 0x04);
        InputReportParser.TryParse(r, ConnectionType.Usb, out var s, out _);
        Assert.Equal(45, s.BatteryPercent);
        Assert.Equal(ChargingState.Discharging, s.Charging);
    }

    [Fact]
    public void BluetoothFull_IsShiftedByOne()
    {
        var r = BtFullReport((r, b) =>
        {
            r[b + 0] = 1; r[b + 5] = 99;
            r[b + 7] = 0x80;          // Triangle, D-pad nahoru (0)
            r[b + 9] = 0x02;          // touchpad
            r[b + 52] = 0x2A;         // plně nabito
        });
        Assert.Equal(0x2A, r[54]);

        Assert.True(InputReportParser.TryParse(r, ConnectionType.Bluetooth, out var s, out var full));
        Assert.True(full);
        Assert.Equal((byte)1, s.LeftX);
        Assert.Equal((byte)99, s.R2);
        Assert.Equal(DsButtons.Triangle | DsButtons.DpadUp | DsButtons.Touchpad, s.Buttons);
        Assert.Equal(100, s.BatteryPercent);
        Assert.Equal(ChargingState.Full, s.Charging);
    }

    [Fact]
    public void BluetoothFull_CorruptedReportIsRejected()
    {
        var r = BtFullReport((_, _) => { });
        r[10] ^= 0x01; // přehozený bit tlačítka po cestě
        Assert.False(InputReportParser.TryParse(r, ConnectionType.Bluetooth, out _, out _));
    }

    [Fact]
    public void BluetoothSimple_ParsesWithoutBattery()
    {
        var r = new byte[78];
        r[0] = 0x01;
        r[1] = 5; r[2] = 6; r[3] = 7; r[4] = 8;
        r[5] = 0x10 | 0x07; // Square + D-pad nahoru-vlevo
        r[6] = 0x80;        // R3
        r[7] = 0x00;
        r[8] = 11; r[9] = 12;

        Assert.True(InputReportParser.TryParse(r, ConnectionType.Bluetooth, out var s, out var full));
        Assert.False(full);
        Assert.Equal((byte)5, s.LeftX);
        Assert.Equal((byte)11, s.L2);
        Assert.Equal((byte)12, s.R2);
        Assert.Equal(DsButtons.Square | DsButtons.DpadUp | DsButtons.DpadLeft | DsButtons.R3, s.Buttons);
        Assert.Null(s.BatteryPercent);
    }

    [Theory]
    [InlineData(0, DsButtons.DpadUp)]
    [InlineData(1, DsButtons.DpadUp | DsButtons.DpadRight)]
    [InlineData(3, DsButtons.DpadDown | DsButtons.DpadRight)]
    [InlineData(4, DsButtons.DpadDown)]
    [InlineData(5, DsButtons.DpadDown | DsButtons.DpadLeft)]
    [InlineData(6, DsButtons.DpadLeft)]
    [InlineData(8, DsButtons.None)]
    public void Dpad_HatValues(byte hat, DsButtons expected)
    {
        Assert.Equal(expected, InputReportParser.ParseButtons(hat, 0, 0));
    }

    [Fact]
    public void UnknownReportId_Rejected()
    {
        var r = new byte[64];
        r[0] = 0x05;
        Assert.False(InputReportParser.TryParse(r, ConnectionType.Usb, out _, out _));
    }

    [Theory]
    [InlineData(0x0A, 100, ChargingState.Discharging)]
    [InlineData(0x00, 5, ChargingState.Discharging)]
    [InlineData(0x23, 100, ChargingState.Full)]
    public void Battery_Values(byte status, int pct, ChargingState cs)
    {
        var (p, s) = InputReportParser.ParseBattery(status);
        Assert.Equal(pct, p);
        Assert.Equal(cs, s);
    }

    [Fact]
    public void Battery_Error()
    {
        var (p, s) = InputReportParser.ParseBattery(0xB0);
        Assert.Null(p);
        Assert.Equal(ChargingState.Error, s);
    }
}
