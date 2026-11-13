namespace DualSenseBridge.DualSense;

[Flags]
public enum DsButtons : uint
{
    None = 0,
    Square = 1 << 0,
    Cross = 1 << 1,
    Circle = 1 << 2,
    Triangle = 1 << 3,
    L1 = 1 << 4,
    R1 = 1 << 5,
    L2 = 1 << 6,
    R2 = 1 << 7,
    Create = 1 << 8,
    Options = 1 << 9,
    L3 = 1 << 10,
    R3 = 1 << 11,
    PS = 1 << 12,
    Touchpad = 1 << 13,
    Mute = 1 << 14,
    DpadUp = 1 << 15,
    DpadRight = 1 << 16,
    DpadDown = 1 << 17,
    DpadLeft = 1 << 18,
}

public enum ConnectionType
{
    Usb,
    Bluetooth,
}

public enum ChargingState
{
    Unknown,
    Discharging,
    Charging,
    Full,
    Error,
}

/// <summary>Jeden okamžitý stav ovladače přečtený z input reportu.</summary>
public readonly record struct DualSenseState(
    byte LeftX,
    byte LeftY,
    byte RightX,
    byte RightY,
    byte L2,
    byte R2,
    DsButtons Buttons,
    int? BatteryPercent,
    ChargingState Charging)
{
    public bool IsPressed(DsButtons b) => (Buttons & b) == b;
}
