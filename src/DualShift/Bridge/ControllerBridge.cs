using DualShift.DualSense;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace DualShift.Bridge;

/// <summary>Stav jednoho připojeného ovladače pro UI.</summary>
public sealed record ControllerStatus(
    string Id, string Name, ConnectionType Connection, int? BatteryPercent, ChargingState Charging, int PlayerIndex);

/// <summary>Jeden DualSense ↔ jeden virtuální Xbox 360 ovladač.</summary>
public sealed class ControllerBridge : IDisposable
{
    private readonly IXbox360Controller _pad;
    private readonly object _lock = new();
    private AppSettings _settings;
    private XInputState? _lastSent;
    private byte _largeMotor, _smallMotor;
    private volatile bool _l2Pressed, _r2Pressed;
    private int _playerIndex = -1;
    private int? _battery;
    private ChargingState _charging = ChargingState.Unknown;
    private DateTime _testRumbleUntil = DateTime.MinValue;

    public DualSenseDevice Device { get; }

    /// <summary>Změna baterie / nabíjení / čísla hráče (volá se z vlákna ovladače).</summary>
    public event Action<ControllerBridge>? StatusChanged;

    public ControllerBridge(DualSenseDevice device, ViGEmClient client, AppSettings settings)
    {
        Device = device;
        _settings = settings;

        _pad = client.CreateXbox360Controller();
        _pad.AutoSubmitReport = false;
        _pad.FeedbackReceived += OnFeedback;
        _pad.Connect();

        device.StateReceived += OnState;
        UpdateOutput();
    }

    public ControllerStatus Status => new(
        Device.DevicePath, Device.DisplayName, Device.Connection, _battery, _charging, _playerIndex);

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _lastSent = null; // např. změna touchpad→Back
        UpdateOutput();
    }

    /// <summary>Krátké zavibrování pro vyzkoušení z nastavení.</summary>
    public void TestRumble()
    {
        _testRumbleUntil = DateTime.UtcNow.AddMilliseconds(600);
        UpdateOutput();
        Task.Delay(650).ContinueWith(_ => UpdateOutput());
    }

    private sealed record StateBox(DualSenseState Value);
    private volatile StateBox? _lastState;

    /// <summary>Poslední přijatý stav (pro test tlačítek v nastavení).</summary>
    public DualSenseState? LastState => _lastState?.Value;

    private void OnState(DualSenseDevice _, DualSenseState state)
    {
        var box = _lastState;
        if (box is null || box.Value != state) _lastState = new StateBox(state);

        var x = XInputMapper.Map(state, _settings.TouchpadAsBack);
        if (x != _lastSent)
        {
            _pad.SetButtonsFull(x.Buttons);
            _pad.SetSliderValue(Xbox360Slider.LeftTrigger, x.LeftTrigger);
            _pad.SetSliderValue(Xbox360Slider.RightTrigger, x.RightTrigger);
            _pad.SetAxisValue(Xbox360Axis.LeftThumbX, x.LeftX);
            _pad.SetAxisValue(Xbox360Axis.LeftThumbY, x.LeftY);
            _pad.SetAxisValue(Xbox360Axis.RightThumbX, x.RightX);
            _pad.SetAxisValue(Xbox360Axis.RightThumbY, x.RightY);
            try { _pad.SubmitReport(); } catch { /* pad se právě odpojuje */ }
            _lastSent = x;
        }

        // Při zapnutých vibracích triggerů podle hry hlídat, který trigger je zmáčknutý.
        bool l2 = TriggerRumble.IsPressed(state.L2, _l2Pressed);
        bool r2 = TriggerRumble.IsPressed(state.R2, _r2Pressed);
        if (l2 != _l2Pressed || r2 != _r2Pressed)
        {
            _l2Pressed = l2;
            _r2Pressed = r2;
            if (_settings.TriggerRumble) UpdateOutput();
        }

        // Zkrácený BT report baterii nenese – ponechat poslední známou hodnotu.
        if (state.Charging == ChargingState.Unknown && state.BatteryPercent is null) return;
        if (state.BatteryPercent != _battery || state.Charging != _charging)
        {
            bool lowChanged = IsLowBattery(_battery, _charging) != IsLowBattery(state.BatteryPercent, state.Charging);
            _battery = state.BatteryPercent;
            _charging = state.Charging;
            if (lowChanged) UpdateOutput();
            StatusChanged?.Invoke(this);
        }
    }

    private void OnFeedback(object sender, Xbox360FeedbackReceivedEventArgs e)
    {
        _largeMotor = e.LargeMotor;
        _smallMotor = e.SmallMotor;
        bool playerChanged = e.LedNumber != _playerIndex;
        _playerIndex = e.LedNumber;
        UpdateOutput();
        if (playerChanged) StatusChanged?.Invoke(this);
    }

    private static bool IsLowBattery(int? pct, ChargingState cs) =>
        pct is < 15 && cs == ChargingState.Discharging;

    private void UpdateOutput()
    {
        lock (_lock)
        {
            var s = _settings;
            byte large = _largeMotor, small = _smallMotor;
            if (DateTime.UtcNow < _testRumbleUntil)
            {
                large = 200;
                small = 120;
            }

            byte left = 0, right = 0;
            if (s.VibrationEnabled)
            {
                // Síla přesně podle hry (už obsahuje nastavení vibrací ve hře).
                left = large;
                right = small;
            }

            // Vibrace triggerů úměrná síle vibrací hry (náhrada impulsních triggerů).
            var (lt, rt) = TriggerRumble.Compose(s, Math.Max(large, small), _l2Pressed, _r2Pressed);

            int rgb = Lightbar.Compute(s, IsLowBattery(_battery, _charging));

            Device.SetOutput(new OutputState
            {
                MotorLeft = left,
                MotorRight = right,
                LeftTrigger = lt,
                RightTrigger = rt,
                LightbarR = (byte)(rgb >> 16),
                LightbarG = (byte)(rgb >> 8),
                LightbarB = (byte)rgb,
                PlayerLeds = s.PlayerLeds ? OutputReportBuilder.PlayerLedsFor(_playerIndex) : (byte)0,
            });
        }
    }

    public void Dispose()
    {
        Device.StateReceived -= OnState;
        _pad.FeedbackReceived -= OnFeedback;
        try { _pad.Disconnect(); } catch { /* už odpojený */ }
        Device.Dispose();
    }
}
