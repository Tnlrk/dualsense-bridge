using DualShift.Localization;
using DualShift.DualSense;

namespace DualShift.UI;

/// <summary>Texty pro stav ovladače.</summary>
public static class Describe
{
    public static string Connection(ConnectionType c) => Loc.T(c == ConnectionType.Bluetooth ? "conn_bt" : "conn_usb");

    public static string Charging(ChargingState s) => s switch
    {
        ChargingState.Charging => Loc.T("charge_charging"),
        ChargingState.Full => Loc.T("charge_full"),
        ChargingState.Discharging => Loc.T("charge_discharging"),
        ChargingState.Error => Loc.T("charge_error"),
        _ => "",
    };

    public static string Battery(int? pct, ChargingState s)
    {
        if (pct is null) return Loc.T(s == ChargingState.Error ? "battery_error" : "battery_unknown");
        var cs = Charging(s);
        return cs.Length > 0 ? Loc.F("battery_pctState", pct, cs) : Loc.F("battery_pct", pct);
    }
}
