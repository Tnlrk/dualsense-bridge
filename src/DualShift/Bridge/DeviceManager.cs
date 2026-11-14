using System.Windows.Forms;
using DualShift.DualSense;
using HidSharp;
using Nefarius.ViGEm.Client;

namespace DualShift.Bridge;

/// <summary>
/// Hlídá připojené DualSense ovladače a ke každému drží <see cref="ControllerBridge"/>.
/// Všechny události se volají na UI vlákně (SynchronizationContext z konstruktoru).
/// </summary>
public sealed class DeviceManager : IDisposable
{
    // Běžně stačí událost o změně zařízení; časovač je jen pojistka a opakuje pokusy u ovladačů,
    // které se nepodařilo otevřít (např. těsně po připojení přes Bluetooth).
    private const int IdlePollMs = 10_000, RetryPollMs = 2_000;

    private readonly ViGEmClient _client;
    private readonly SynchronizationContext _ui;
    private readonly Dictionary<string, ControllerBridge> _bridges = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _pollTimer;
    private AppSettings _settings;
    private bool _scanQueued;
    private bool _disposed;

    public event Action<ControllerStatus>? ControllerConnected;
    public event Action<ControllerStatus>? ControllerDisconnected;
    public event Action<ControllerStatus>? ControllerStatusChanged;
    /// <summary>
    /// DualSense, který nejde otevřít (pravděpodobně ho skrývá HidHide a aplikace není na whitelistu).
    /// Hlásí se jednou; další pokusy o otevření probíhají potichu.
    /// </summary>
    public event Action<string>? ControllerInaccessible;

    /// <exception cref="Nefarius.ViGEm.Client.Exceptions.VigemBusNotFoundException">Chybí ovladač ViGEmBus.</exception>
    public DeviceManager(AppSettings settings)
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settings = settings.Clone();
        _client = new ViGEmClient();

        DeviceList.Local.Changed += OnDeviceListChanged;
        _pollTimer = new System.Windows.Forms.Timer { Interval = IdlePollMs };
        _pollTimer.Tick += (_, _) => Scan();
        _pollTimer.Start();
    }

    public IReadOnlyList<ControllerStatus> Controllers =>
        _bridges.Values.Select(b => b.Status).OrderBy(s => s.PlayerIndex < 0 ? 99 : s.PlayerIndex).ToList();

    public IEnumerable<string> ConnectedDevicePaths => _bridges.Keys.ToList();

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings.Clone();
        foreach (var b in _bridges.Values) b.ApplySettings(_settings);
    }

    /// <summary>Živý stav ovladače podle Id (cesty zařízení).</summary>
    public DualSenseState? GetState(string id) =>
        _bridges.TryGetValue(id, out var b) ? b.LastState : null;

    public void TestRumble()
    {
        foreach (var b in _bridges.Values) b.TestRumble();
    }

    private void OnDeviceListChanged(object? sender, DeviceListChangedEventArgs e)
    {
        // Volá se z cizího vlákna, často několikrát za sebou.
        _ui.Post(_ =>
        {
            if (_scanQueued || _disposed) return;
            _scanQueued = true;
            Task.Delay(300).ContinueWith(_ => _ui.Post(__ => { _scanQueued = false; Scan(); }, null));
        }, null);
    }

    /// <summary>Najde nové ovladače a připojí je. Volat na UI vlákně.</summary>
    public void Scan()
    {
        if (_disposed) return;
        List<HidDevice> devices;
        try { devices = DualSenseDevice.Enumerate().ToList(); }
        catch { return; }

        var present = new HashSet<string>(devices.Select(d => d.DevicePath), StringComparer.OrdinalIgnoreCase);
        _failedPaths.IntersectWith(present);

        foreach (var hid in devices)
        {
            string path = hid.DevicePath;
            if (_bridges.ContainsKey(path)) continue;

            var ds = DualSenseDevice.TryOpen(hid);
            ControllerBridge? bridge = null;
            if (ds is not null)
            {
                try { bridge = new ControllerBridge(ds, _client, _settings); }
                catch { ds.Dispose(); }
            }
            if (ds is null || bridge is null)
            {
                // Ohlásit jen poprvé; časovač to pak zkouší znovu.
                if (_failedPaths.Add(path) && ds is null) ControllerInaccessible?.Invoke(path);
                continue;
            }

            _failedPaths.Remove(path);
            _bridges[path] = bridge;
            ds.Disconnected += d => _ui.Post(_ => Remove(d.DevicePath), null);
            bridge.StatusChanged += b => _ui.Post(_ =>
            {
                if (_bridges.ContainsValue(b)) ControllerStatusChanged?.Invoke(b.Status);
            }, null);
            ControllerConnected?.Invoke(bridge.Status);
            // Čtení spustit až po přihlášení k událostem, aby se žádné odpojení neztratilo.
            ds.Start();
        }

        _pollTimer.Interval = _failedPaths.Count > 0 ? RetryPollMs : IdlePollMs;
    }

    private void Remove(string path)
    {
        if (!_bridges.Remove(path, out var bridge)) return;
        var status = bridge.Status;
        bridge.Dispose();
        ControllerDisconnected?.Invoke(status);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DeviceList.Local.Changed -= OnDeviceListChanged;
        _pollTimer.Dispose();
        foreach (var b in _bridges.Values) b.Dispose();
        _bridges.Clear();
        _client.Dispose();
    }
}
