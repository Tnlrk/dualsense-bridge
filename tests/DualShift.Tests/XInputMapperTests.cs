using DualShift.Bridge;
using DualShift.DualSense;

namespace DualShift.Tests;

public class XInputMapperTests
{
    private static DualSenseState State(DsButtons b, byte lx = 128, byte ly = 128, byte l2 = 0, byte r2 = 0) =>
        new(lx, ly, 128, 128, l2, r2, b, null, ChargingState.Unknown);

    [Fact]
    public void FaceButtons_MapToXboxLayout()
    {
        var x = XInputMapper.Map(State(DsButtons.Cross | DsButtons.Circle | DsButtons.Square | DsButtons.Triangle), false);
        Assert.Equal(XInputMapper.A | XInputMapper.B | XInputMapper.X | XInputMapper.Y, x.Buttons);
    }

    [Fact]
    public void MenuButtons()
    {
        var x = XInputMapper.Map(State(DsButtons.Create | DsButtons.Options | DsButtons.PS), false);
        Assert.Equal(XInputMapper.Back | XInputMapper.Start | XInputMapper.Guide, x.Buttons);
    }

    [Fact]
    public void Touchpad_OptionallyBack()
    {
        Assert.Equal(0, XInputMapper.Map(State(DsButtons.Touchpad), false).Buttons);
        Assert.Equal(XInputMapper.Back, XInputMapper.Map(State(DsButtons.Touchpad), true).Buttons);
    }

    [Fact]
    public void Sticks_FullRangeAndInvertedY()
    {
        var up = XInputMapper.Map(State(DsButtons.None, lx: 0, ly: 0), false);
        Assert.Equal(short.MinValue, up.LeftX);
        Assert.Equal(short.MaxValue, up.LeftY); // páčka nahoru = kladné Y v XInput

        var down = XInputMapper.Map(State(DsButtons.None, lx: 255, ly: 255), false);
        Assert.Equal(short.MaxValue, down.LeftX);
        Assert.Equal(short.MinValue, down.LeftY);
    }

    [Theory]
    [InlineData(DsButtons.Cross, "A")]
    [InlineData(DsButtons.Triangle, "Y")]
    [InlineData(DsButtons.Options, "Start")]
    [InlineData(DsButtons.PS, "Guide")]
    [InlineData(DsButtons.R2, "RT")]
    [InlineData(DsButtons.Mute, null)]
    public void XboxLabels(DsButtons b, string? expected)
    {
        Assert.Equal(expected, XInputMapper.XboxLabel(b, touchpadAsBack: true));
    }

    [Fact]
    public void TouchpadLabel_FollowsSetting()
    {
        Assert.Equal("Back", XInputMapper.XboxLabel(DsButtons.Touchpad, true));
        Assert.Null(XInputMapper.XboxLabel(DsButtons.Touchpad, false));
    }

    [Fact]
    public void Triggers_PassThrough()
    {
        var x = XInputMapper.Map(State(DsButtons.None, l2: 17, r2: 255), false);
        Assert.Equal(17, x.LeftTrigger);
        Assert.Equal(255, x.RightTrigger);
    }
}
