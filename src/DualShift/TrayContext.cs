using DualShift.Localization;
using System.Drawing;
using System.Windows.Forms;
using DualShift.Bridge;
using DualShift.DualSense;
using DualShift.HidHide;
using DualShift.Setup;
using DualShift.UI;
using Nefarius.ViGEm.Client.Exceptions;

namespace DualShift;

/// <summary>Aplikace bez hlavního okna – jen ikona v oznamovací oblasti.</summary>
public sealed class TrayContext : ApplicationContext
{
    private const string AppName = "DualShift";

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly NotifyIcon _tray;
    private readonly TrayIconRenderer _renderer = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _statusHeader = new() { Enabled = false };
    private readonly ToolStripSeparator _statusSeparator = new();
    private readonly ToolStripMenuItem _settingsItem = new();
    private readonly ToolStripMenuItem _helpItem = new();
    private readonly ToolStripMenuItem _vibrationItem = new();
    private readonly ToolStripMenuItem _hideItem = new();
    private readonly ToolStripMenuItem _autostartItem = new();
    private readonly ToolStripMenuItem _installItem = new();
    private readonly ToolStripMenuItem _languageItem = new("Jazyk / Language");
    private readonly ToolStripMenuItem _langCs = new("Čeština");
    private readonly ToolStripMenuItem _langEn = new("English");
    private readonly ToolStripMenuItem _exitItem = new();
    private readonly List<ToolStripItem> _controllerItems = new();
    private readonly Dictionary<string, bool> _lowBatteryNotified = new();
    private readonly Dictionary<string, ChargingState> _lastCharging = new();
    private readonly HashSet<string> _hideDeclined = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 500 };
    private DeviceManager? _devices;
    private SettingsWindow? _settingsForm;
    private HelpWindow? _helpWindow;
    private bool _whitelistRepairTried;
    private bool _busy;
    private readonly EventWaitHandle _showEvent;
    private readonly bool _startMinimized;
    private RegisteredWaitHandle? _showWait;

    /// <param name="showEvent">Signál od dalšího spuštění aplikace – ukázat okno.</param>
    /// <param name="startMinimized">Spuštěno s Windows – zůstat jen v liště.</param>
    public TrayContext(EventWaitHandle showEvent, bool startMinimized)
    {
        _showEvent = showEvent;
        _startMinimized = startMinimized;
        Loc.SetLanguage(_settings.Language);
        Loc.LanguageChanged += () => { UpdateMenu(); UpdateTray(); };
        BuildMenu();
        UpdateMenu();
        _tray = new NotifyIcon { ContextMenuStrip = _menu, Visible = true, Text = AppName };
        _tray.DoubleClick += (_, _) => ShowSettings();
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _settings.Save(); };

        UpdateTray();
        // Spustit až po naběhnutí smyčky zpráv (kvůli dialogům a SynchronizationContext).
        var startup = new System.Windows.Forms.Timer { Interval = 50 };
        startup.Tick += async (_, _) =>
        {
            startup.Stop();
            startup.Dispose();
            await StartAsync();
        };
        startup.Start();
    }

    // ------------------------------------------------------------------ start

    private async Task StartAsync()
    {
        // Další spuštění exe (např. dvojklik, když už běží) jen přenese okno do popředí.
        var ui = SynchronizationContext.Current!;
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => ui.Post(_ => ShowSettings(), null), null, Timeout.Infinite, executeOnlyOnce: false);

        // Při ručním spuštění se rovnou otevře okno; se startem Windows jen ikona v liště.
        if (!_startMinimized) ShowSettings();

        RefreshInstalledCopy();
        if (!await EnsureViGEmAsync()) return;
        StartDevices();
        await OfferHidHideOnceAsync();
    }

    private async Task<bool> EnsureViGEmAsync()
    {
        if (DependencyInstaller.IsInstalled(Dependency.ViGEmBus)) return true;

        var answer = MessageBox.Show(
            Loc.T("dlg_vigemNeeded"),
            AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            UpdateTray();
            return false;
        }
        return await InstallAsync(Dependency.ViGEmBus);
    }

    private async Task<bool> InstallAsync(Dependency d)
    {
        if (_busy) return false;
        _busy = true;
        try
        {
            Notify(Loc.T("note_installTitle"), Loc.F("note_installing", DependencyInstaller.DisplayName(d)), ToolTipIcon.Info, force: true);
            var result = await DependencyInstaller.InstallAsync(d);
            if (!result.Success)
            {
                MessageBox.Show(Loc.F("dlg_installFailed", DependencyInstaller.DisplayName(d), result.Error),
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (result.RestartRequired)
            {
                MessageBox.Show(Loc.F("dlg_installRestart", DependencyInstaller.DisplayName(d)),
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return DependencyInstaller.IsInstalled(d);
            }
            return true;
        }
        finally
        {
            _busy = false;
            UpdateTray();
        }
    }

    private void StartDevices()
    {
        if (_devices is not null) return;
        try
        {
            _devices = new DeviceManager(_settings);
        }
        catch (VigemBusNotFoundException)
        {
            UpdateTray();
            return;
        }

        _devices.ControllerConnected += OnConnected;
        _devices.ControllerDisconnected += OnDisconnected;
        _devices.ControllerStatusChanged += OnStatusChanged;
        _devices.ControllerInaccessible += OnInaccessible;
        _devices.Scan();
        UpdateTray();
    }

    private async Task OfferHidHideOnceAsync()
    {
        if (_settings.HidHideOffered || DependencyInstaller.IsInstalled(Dependency.HidHide)) return;
        _settings.HidHideOffered = true;
        _settings.Save();

        var answer = MessageBox.Show(
            Loc.T("dlg_hidhideOffer"),
            AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer == DialogResult.Yes && await InstallAsync(Dependency.HidHide))
            await SetHideOriginalAsync(true);
    }

    /// <summary>Při zapnutém autostartu aktualizovat kopii v %LocalAppData%, pokud běží jiná verze exe.</summary>
    private static void RefreshInstalledCopy()
    {
        try
        {
            Autostart.MigrateFromLegacyName();
            // Obnoví kopii v AppData (pokud běží jiná verze exe) i záznam v registru (spouštění do lišty).
            if (Autostart.IsEnabled) Autostart.Enable();
        }
        catch { /* kopie se nepovedla – nevadí */ }
    }

    // ------------------------------------------------------------------ menu

    private void BuildMenu()
    {
        _settingsItem.Font = new Font(_menu.Font, FontStyle.Bold);
        _settingsItem.Click += (_, _) => ShowSettings();
        _helpItem.Click += (_, _) => ShowHelp();
        _langCs.Click += (_, _) => SetLanguage(Loc.Czech);
        _langEn.Click += (_, _) => SetLanguage(Loc.English);
        _languageItem.DropDownItems.Add(_langCs);
        _languageItem.DropDownItems.Add(_langEn);
        _exitItem.Click += (_, _) => ExitThread();
        _vibrationItem.Click += (_, _) =>
        {
            _settings.VibrationEnabled = !_settings.VibrationEnabled;
            OnSettingsChanged();
            _settingsForm?.RefreshFromSettings();
        };
        _hideItem.Click += async (_, _) => await SetHideOriginalAsync(!_settings.HideOriginal);
        _autostartItem.Click += (_, _) => ToggleAutostart();
        _installItem.Click += async (_, _) =>
        {
            if (await InstallAsync(Dependency.ViGEmBus)) StartDevices();
        };

        _menu.Items.Add(_statusHeader);
        _menu.Items.Add(_statusSeparator);
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_helpItem);
        _menu.Items.Add(_vibrationItem);
        _menu.Items.Add(_hideItem);
        _menu.Items.Add(_autostartItem);
        _menu.Items.Add(_installItem);
        _menu.Items.Add(_languageItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_exitItem);
        _menu.Opening += (_, _) => UpdateMenu();
    }

    private void UpdateMenu()
    {
        _settingsItem.Text = Loc.T("tray_settings");
        _helpItem.Text = Loc.T("tray_help");
        _vibrationItem.Text = Loc.T("tray_vibration");
        _hideItem.Text = Loc.T("tray_hide");
        _autostartItem.Text = Loc.T("tray_autostart");
        _installItem.Text = Loc.T("tray_installVigem");
        _exitItem.Text = Loc.T("tray_exit");
        _langCs.Checked = Loc.Language == Loc.Czech;
        _langEn.Checked = Loc.Language == Loc.English;

        _vibrationItem.Checked = _settings.VibrationEnabled;
        _hideItem.Checked = _settings.HideOriginal;
        _autostartItem.Checked = Autostart.IsEnabled;
        _installItem.Visible = _devices is null;

        foreach (var item in _controllerItems)
        {
            _menu.Items.Remove(item);
            item.Image?.Dispose();
            item.Dispose();
        }
        _controllerItems.Clear();

        var list = _devices?.Controllers ?? Array.Empty<ControllerStatus>();
        if (_devices is null)
        {
            _statusHeader.Text = Loc.T("tray_missingVigem");
        }
        else if (list.Count == 0)
        {
            _statusHeader.Text = Loc.T("tray_noController");
        }
        else
        {
            _statusHeader.Text = list.Count == 1 ? Loc.T("tray_oneController") : Loc.F("tray_nControllers", list.Count);
            int idx = _menu.Items.IndexOf(_statusSeparator);
            foreach (var c in list)
            {
                var item = new ToolStripMenuItem(
                    $"{c.Name} · {Describe.Connection(c.Connection)} · {Describe.Battery(c.BatteryPercent, c.Charging)}")
                {
                    Enabled = false,
                    Image = _renderer.RenderBitmap(c),
                };
                _menu.Items.Insert(idx++, item);
                _controllerItems.Add(item);
            }
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is null)
        {
            _settingsForm = new SettingsWindow(_settings);
            // Klávesnice (šipky u posuvníků) ve WPF okně spuštěném z WinForms smyčky.
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(_settingsForm);
            _settingsForm.StateProvider = id => _devices?.GetState(id);
            _settingsForm.SettingsChanged += (_, _) => OnSettingsChanged();
            _settingsForm.HelpRequested += (_, _) => ShowHelp();
            _settingsForm.HiddenToTray += (_, _) => ShowTrayHintOnce();
            _settingsForm.LanguageRequested += (_, lang) => SetLanguage(lang);
            _settingsForm.TestRumbleRequested += (_, _) =>
            {
                if (!_settings.VibrationEnabled)
                    Notify(Loc.T("note_vibrationOffTitle"), Loc.T("note_vibrationOffText"), ToolTipIcon.Info, force: true);
                _devices?.TestRumble();
            };
        }
        _settingsForm.UpdateControllers(_devices?.Controllers ?? Array.Empty<ControllerStatus>());
        _settingsForm.ShowAndActivate();
    }

    /// <summary>Po prvním zavření okna vysvětlit, že aplikace běží dál v liště.</summary>
    private void ShowTrayHintOnce()
    {
        if (_settings.TrayHintShown) return;
        _settings.TrayHintShown = true;
        _settings.Save();
        Notify(Loc.T("note_trayHintTitle"), Loc.T("note_trayHintText"), ToolTipIcon.Info, force: true);
    }

    private void ShowHelp()
    {
        if (_helpWindow is null)
        {
            _helpWindow = new HelpWindow();
            System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(_helpWindow);
        }
        _helpWindow.ShowAndActivate();
    }

    private void SetLanguage(string lang)
    {
        _settings.Language = lang;
        _settings.Save();
        Loc.SetLanguage(lang);
    }

    private void OnSettingsChanged()
    {
        _devices?.ApplySettings(_settings);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void ToggleAutostart()
    {
        try
        {
            if (Autostart.IsEnabled)
            {
                Autostart.Disable();
            }
            else
            {
                Autostart.Enable();
                Notify(Loc.T("note_autostartTitle"), Loc.T("note_autostartText"), ToolTipIcon.Info, force: true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.F("dlg_autostartFailed", ex.Message), AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ------------------------------------------------------------------ HidHide

    private async Task SetHideOriginalAsync(bool hide)
    {
        if (_busy) return;
        if (hide && !DependencyInstaller.IsInstalled(Dependency.HidHide))
        {
            var answer = MessageBox.Show(
                Loc.T("dlg_hidhideNeeded"),
                AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes || !await InstallAsync(Dependency.HidHide)) return;
        }

        var ids = hide
            ? ConnectedInstanceIds().ToList()
            : _settings.HiddenInstanceIds.ToList();

        if (!await RunHidHideAsync(hide, ids)) return;

        _settings.HideOriginal = hide;
        if (hide)
        {
            foreach (var id in ids)
                if (!_settings.HiddenInstanceIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                    _settings.HiddenInstanceIds.Add(id);
            _hideDeclined.Clear();
            Notify(Loc.T("note_hiddenTitle"), Loc.T("note_hiddenText"), ToolTipIcon.Info, force: true);
        }
        else
        {
            _settings.HiddenInstanceIds.Clear();
            Notify(Loc.T("note_visibleTitle"), Loc.T("note_visibleText"), ToolTipIcon.Info, force: true);
        }
        _settings.Save();
    }

    private async Task<bool> RunHidHideAsync(bool hide, List<string> ids)
    {
        _busy = true;
        try
        {
            string? error = null;
            bool ok = await Task.Run(() => HidHideService.RunElevated(hide, ids, out error));
            if (!ok)
                Notify("HidHide", Loc.F("note_hidhideFailed", error), ToolTipIcon.Warning, force: true);
            return ok;
        }
        finally
        {
            _busy = false;
        }
    }

    private IEnumerable<string> ConnectedInstanceIds() =>
        (_devices?.ConnectedDevicePaths ?? Enumerable.Empty<string>())
            .Select(HidHideService.InstanceIdFromDevicePath)
            .Where(id => id is not null)
            .Select(id => id!);

    /// <summary>Nový ovladač (nebo stejný přes jiné spojení) při zapnutém skrývání → skrýt i jeho.</summary>
    private async Task HideNewControllerAsync(string devicePath)
    {
        if (!_settings.HideOriginal || _busy) return;
        var id = HidHideService.InstanceIdFromDevicePath(devicePath);
        if (id is null || _hideDeclined.Contains(id) ||
            _settings.HiddenInstanceIds.Contains(id, StringComparer.OrdinalIgnoreCase)) return;
        if (!DependencyInstaller.IsInstalled(Dependency.HidHide)) return;

        if (await RunHidHideAsync(true, new List<string> { id }))
        {
            _settings.HiddenInstanceIds.Add(id);
            _settings.Save();
        }
        else
        {
            _hideDeclined.Add(id);
        }
    }

    private async void OnInaccessible(string devicePath)
    {
        // Typicky: ovladač je skrytý HidHide a tato kopie exe není na whitelistu (exe bylo přesunuto).
        if (_settings.HideOriginal && !_whitelistRepairTried && DependencyInstaller.IsInstalled(Dependency.HidHide))
        {
            _whitelistRepairTried = true;
            if (await RunHidHideAsync(true, new List<string>()))
            {
                await Task.Delay(500);
                _devices?.Scan();
            }
            return;
        }
        Notify(Loc.T("note_inaccessibleTitle"), Loc.T("note_inaccessibleText"), ToolTipIcon.Warning);
    }

    // ------------------------------------------------------------------ události ovladačů

    private async void OnConnected(ControllerStatus c)
    {
        Notify(Loc.F("note_connectedTitle", c.Name), Loc.F("note_connectedText", Describe.Connection(c.Connection)), ToolTipIcon.Info);
        _lastCharging[c.Id] = c.Charging;
        UpdateTray();
        await HideNewControllerAsync(c.Id);
    }

    private void OnDisconnected(ControllerStatus c)
    {
        Notify(Loc.F("note_disconnectedTitle", c.Name), Loc.F("note_disconnectedText", Describe.Connection(c.Connection)), ToolTipIcon.None);
        _lowBatteryNotified.Remove(c.Id);
        _lastCharging.Remove(c.Id);
        UpdateTray();
    }

    private void OnStatusChanged(ControllerStatus c)
    {
        // Slabá baterie – upozornit jednou za vybití.
        if (c.BatteryPercent is < 15 && c.Charging == ChargingState.Discharging)
        {
            if (!_lowBatteryNotified.GetValueOrDefault(c.Id))
            {
                _lowBatteryNotified[c.Id] = true;
                Notify(Loc.T("note_lowBatteryTitle"), Loc.F("note_lowBatteryText", c.Name, c.BatteryPercent), ToolTipIcon.Warning);
            }
        }
        else if (c.Charging != ChargingState.Discharging || c.BatteryPercent >= 20)
        {
            _lowBatteryNotified[c.Id] = false;
        }

        var prev = _lastCharging.GetValueOrDefault(c.Id);
        if (c.Charging == ChargingState.Full && prev == ChargingState.Charging)
            Notify(Loc.T("note_chargedTitle"), Loc.F("note_chargedText", c.Name), ToolTipIcon.Info);
        _lastCharging[c.Id] = c.Charging;

        UpdateTray();
    }

    // ------------------------------------------------------------------ ikona

    private void UpdateTray()
    {
        var list = _devices?.Controllers ?? Array.Empty<ControllerStatus>();
        // Ikona ukazuje ovladač s nejslabší baterií.
        var shown = list.OrderBy(c => c.BatteryPercent ?? 101).FirstOrDefault();

        TrayIconRenderer.Mode mode = _devices is null ? TrayIconRenderer.Mode.Error
            : shown is null ? TrayIconRenderer.Mode.Disconnected
            : TrayIconRenderer.Mode.Connected;
        _tray.Icon = _renderer.Render(mode, shown);

        string text = mode switch
        {
            TrayIconRenderer.Mode.Error => Loc.F("tip_missingVigem", AppName),
            TrayIconRenderer.Mode.Disconnected => Loc.F("tip_noController", AppName),
            _ => string.Join("\n", list.Select(TooltipLine)),
        };
        _tray.Text = text.Length > 127 ? text[..127] : text;

        _settingsForm?.UpdateControllers(list);
    }

    private static string TooltipLine(ControllerStatus c)
    {
        var parts = new List<string> { c.Name, c.Connection == ConnectionType.Bluetooth ? "Bluetooth" : "USB" };
        if (c.BatteryPercent is not null) parts.Add($"{c.BatteryPercent} %");
        var cs = Describe.Charging(c.Charging);
        if (c.Charging is ChargingState.Charging or ChargingState.Full) parts.Add(cs);
        return string.Join(" · ", parts);
    }

    private void Notify(string title, string text, ToolTipIcon icon, bool force = false)
    {
        if (!force && !_settings.Notifications) return;
        _tray.ShowBalloonTip(4000, title, text, icon);
    }

    protected override void ExitThreadCore()
    {
        _showWait?.Unregister(null);
        _saveTimer.Stop();
        _settings.Save();
        _devices?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _renderer.Dispose();
        _settingsForm?.CloseForReal();
        _helpWindow?.CloseForReal();
        base.ExitThreadCore();
    }
}
