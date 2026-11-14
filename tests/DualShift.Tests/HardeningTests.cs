using DualShift.HidHide;

namespace DualShift.Tests;

public class HardeningTests
{
    [Theory]
    [InlineData(@"HID\VID_054C&PID_0CE6&MI_03\7&2a3b4c5d&0&0000", true)]                                   // DualSense USB
    [InlineData(@"HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054C_PID&0CE6\8&1a2b3c&0&0000", true)] // DualSense BT
    [InlineData(@"HID\VID_054C&PID_0DF2&MI_03\7&2a3b4c5d&0&0000", true)]                                   // DualSense Edge
    [InlineData(@"HID\VID_046D&PID_C31C&MI_00\7&1&0&0000", false)]                                         // cizí klávesnice
    [InlineData(@"HID\VID_054C&PID_09CC\7&1&0&0000", false)]                                               // DualShock 4
    [InlineData(@"USB\VID_054C&PID_0CE6\5&1&0&1", false)]                                                  // ne HID
    [InlineData("", false)]
    public void ElevatedHelperAcceptsOnlyDualSense(string instanceId, bool expected)
    {
        Assert.Equal(expected, HidHideService.IsDualSenseInstanceId(instanceId));
    }
}
