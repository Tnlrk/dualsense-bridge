using System.Diagnostics;
using HidSharp;

namespace DualSenseBridge.DualSense;

/// <summary>
/// Otevřený DualSense: čtecí vlákno (input reporty → <see cref="StateReceived"/>)
/// a zapisovací vlákno (poslední požadovaný <see cref="OutputState"/> → výstupní report).
/// </summary>
public sealed class DualSenseDevice : IDisposable
{
    public const int SonyVendorId = 0x054C;
    public const int DualSenseProductId = 0x0CE6;
    public const int DualSenseEdgeProductId = 0x0DF2;

    private const byte FeatureCalibration = 0x05;
    private const byte FeatureFirmwareInfo = 0x20;

    private readonly HidDevice _device;
    private readonly HidStream _stream;
    private readonly CancellationTokenSource _cts = new();
    private readonly AutoResetEvent _outputSignal = new(false);
    private readonly object _outputLock = new();
    private OutputState? _pendingOutput;
    private OutputState? _lastWrittenOutput;
    private Thread? _reader, _writer;
    private bool? _vibrationV2;
    private bool _lightbarReleased;
    private byte _btSequence;
    private int _disconnectedRaised;

    public string DevicePath => _device.DevicePath;
    public ConnectionType Connection { get; }
    public bool IsEdge => _device.ProductID == DualSenseEdgeProductId;
    public string DisplayName => IsEdge ? "DualSense Edge" : "DualSense";

    /// <summary>Volá se z čtecího vlákna pro každý report.</summary>
    public event Action<DualSenseDevice, DualSenseState>? StateReceived;
    /// <summary>Volá se jednou, když se zařízení odpojí nebo přestane odpovídat.</summary>
    public event Action<DualSenseDevice>? Disconnected;

    private DualSenseDevice(HidDevice device, HidStream stream, ConnectionType connection)
    {
        _device = device;
        _stream = stream;
        Connection = connection;
    }

    public static bool IsDualSense(HidDevice d) =>
        d.VendorID == SonyVendorId && d.ProductID is DualSenseProductId or DualSenseEdgeProductId;

    public static IEnumerable<HidDevice> Enumerate() =>
        DeviceList.Local.GetHidDevices(SonyVendorId).Where(IsDualSense);

    /// <summary>Otevře zařízení; vrací null, pokud nejde otevřít (např. ho skrývá HidHide).</summary>
    public static DualSenseDevice? TryOpen(HidDevice device)
    {
        int maxInput;
        try { maxInput = device.GetMaxInputReportLength(); }
        catch { return null; }

        // USB input report má 64 B, Bluetooth 78 B.
        var connection = maxInput > 64 ? ConnectionType.Bluetooth : ConnectionType.Usb;
        if (!device.TryOpen(out HidStream stream)) return null;

        var ds = new DualSenseDevice(device, stream, connection);
        ds.Initialize();
        return ds;
    }

    private void Initialize()
    {
        _stream.ReadTimeout = 1000;
        _stream.WriteTimeout = 1000;

        _vibrationV2 = ReadVibrationV2();
        if (Connection == ConnectionType.Bluetooth)
            RequestFullReports();

        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "DualSense reader" };
        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "DualSense writer" };
        _reader.Start();
        _writer.Start();
    }

    /// <summary>Přečtení kalibrace (feature 0x05) přepne BT ovladač do plného reportu 0x31.</summary>
    private void RequestFullReports()
    {
        try
        {
            var buf = new byte[Math.Max(_device.GetMaxFeatureReportLength(), 41)];
            buf[0] = FeatureCalibration;
            _stream.GetFeature(buf);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Calibration feature read failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Firmware 2.21+ (a všechny Edge) používají nový příznak vibrací.
    /// Vrací null, když verzi nejde zjistit – pak se posílají oba příznaky.
    /// </summary>
    private bool? ReadVibrationV2()
    {
        if (IsEdge) return true;
        try
        {
            var buf = new byte[Math.Max(_device.GetMaxFeatureReportLength(), 64)];
            buf[0] = FeatureFirmwareInfo;
            _stream.GetFeature(buf);
            int updateVersion = buf[44] | (buf[45] << 8);
            return updateVersion >= 0x0215;
        }
        catch
        {
            return null;
        }
    }

    private void ReadLoop()
    {
        var buf = new byte[Math.Max(_device.GetMaxInputReportLength(), 78)];
        var lastFullRequest = Stopwatch.StartNew();
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                int n;
                try { n = _stream.Read(buf, 0, buf.Length); }
                catch (TimeoutException) { continue; }

                if (n <= 0) continue;
                if (!InputReportParser.TryParse(buf.AsSpan(0, n), Connection, out var state, out bool full))
                    continue;

                // BT ovladač po probuzení někdy posílá zkrácené reporty – požádat znovu o plné.
                if (!full && lastFullRequest.ElapsedMilliseconds > 1000)
                {
                    RequestFullReports();
                    lastFullRequest.Restart();
                }

                StateReceived?.Invoke(this, state);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            Debug.WriteLine($"DualSense read ended: {ex.Message}");
        }
        RaiseDisconnected();
    }

    /// <summary>Nastaví požadovaný výstup; odešle se jen při změně.</summary>
    public void SetOutput(OutputState output)
    {
        lock (_outputLock) _pendingOutput = output;
        _outputSignal.Set();
    }

    private void WriteLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            _outputSignal.WaitOne(500);
            if (_cts.IsCancellationRequested) break;

            OutputState? output;
            lock (_outputLock) output = _pendingOutput;
            if (output is null || output.Equals(_lastWrittenOutput)) continue;

            if (!Write(output)) continue;
            _lastWrittenOutput = output;
            Thread.Sleep(8); // nezahltit BT
        }
    }

    private bool Write(OutputState output)
    {
        try
        {
            var report = OutputReportBuilder.Build(output, Connection, _btSequence++, _vibrationV2,
                releaseLightbar: !_lightbarReleased);
            _stream.Write(report);
            _lightbarReleased = true;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DualSense write failed: {ex.Message}");
            return false;
        }
    }

    private void RaiseDisconnected()
    {
        if (Interlocked.Exchange(ref _disconnectedRaised, 1) == 0)
            Disconnected?.Invoke(this);
    }

    public void Dispose()
    {
        if (_cts.IsCancellationRequested) return;
        _cts.Cancel();
        _outputSignal.Set();
        _writer?.Join(1000);

        // Vrátit ovladač do klidu: bez vibrací a efektů triggerů.
        if (_disconnectedRaised == 0)
        {
            var last = _lastWrittenOutput ?? new OutputState();
            Write(last with
            {
                MotorLeft = 0, MotorRight = 0,
                LeftTrigger = TriggerEffect.None, RightTrigger = TriggerEffect.None,
            });
        }

        _disconnectedRaised = 1; // vlastní ukončení – nehlásit odpojení
        _stream.Dispose();
        _reader?.Join(1500);
        _outputSignal.Dispose();
        _cts.Dispose();
    }
}
