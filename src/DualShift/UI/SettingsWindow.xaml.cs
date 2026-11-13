using DualShift.Localization;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DualShift.Bridge;
using DualShift.DualSense;
using SD = System.Drawing;

namespace DualShift.UI;

/// <summary>Moderní okno nastavení – vše na jedné stránce, změny se projeví okamžitě.</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ControllerPainter _painter = new();
    private readonly DispatcherTimer _liveTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private IReadOnlyList<ControllerStatus> _controllers = Array.Empty<ControllerStatus>();
    private string? _selectedId;
    private WriteableBitmap? _frame;
    private string? _lastFrameKey;
    private bool _loading;
    private bool _allowClose;

    private static readonly int[] SwatchColors =
    {
        0x0040FF, 0x00B4FF, 0x00E676, 0xB4FF00, 0xFFD000, 0xFF6A00, 0xFF0000, 0xFF2D9A, 0x8A2BFF, 0xFFFFFF,
    };

    /// <summary>Nastavení se změnilo – aplikovat a uložit.</summary>
    public event EventHandler? SettingsChanged;
    /// <summary>Uživatel chce vyzkoušet vibrace.</summary>
    public event EventHandler? TestRumbleRequested;
    /// <summary>Uživatel chce otevřít nápovědu.</summary>
    public event EventHandler? HelpRequested;
    /// <summary>Uživatel přepnul jazyk ("cs" / "en").</summary>
    public event EventHandler<string>? LanguageRequested;
    /// <summary>Okno se zavřelo křížkem a aplikace běží dál v liště.</summary>
    public event EventHandler? HiddenToTray;
    /// <summary>Živý stav ovladače podle Id – nastavuje TrayContext.</summary>
    public Func<string, DualSenseState?>? StateProvider { get; set; }

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        _painter.Background = SD.Color.FromArgb(0x24, 0x27, 0x2E);

        BuildSwatches();
        LoadFromSettings();

        foreach (var cb in new[] { Vibration, TriggerRumble, TriggerRumbleOnlyPressed, LightbarOn, LowBattery, PlayerLeds, Touchpad, Notifications })
        {
            cb.Checked += (_, _) => Commit();
            cb.Unchecked += (_, _) => Commit();
        }
        LeftTrigger.EffectChanged += (_, _) => Commit();
        RightTrigger.EffectChanged += (_, _) => Commit();
        Brightness.ValueChanged += (_, _) => Commit();
        Hue.ValueChanged += (_, _) => ColorFromSliders();
        Saturation.ValueChanged += (_, _) => ColorFromSliders();
        TestRumble.Click += (_, _) => TestRumbleRequested?.Invoke(this, EventArgs.Empty);
        HelpButton.Click += (_, _) => HelpRequested?.Invoke(this, EventArgs.Empty);
        SyncLanguageButtons();
        LangCs.Checked += (_, _) => { if (Loc.Language != Loc.Czech) LanguageRequested?.Invoke(this, Loc.Czech); };
        LangEn.Checked += (_, _) => { if (Loc.Language != Loc.English) LanguageRequested?.Invoke(this, Loc.English); };
        Loc.LanguageChanged += () =>
        {
            SyncLanguageButtons();
            UpdateControllers(_controllers);
        };

        _liveTimer.Tick += (_, _) => UpdateLiveView();
        IsVisibleChanged += (_, _) => { if (IsVisible) _liveTimer.Start(); else _liveTimer.Stop(); };
        ImageHost.SizeChanged += (_, _) => { _lastFrameKey = null; UpdateLiveView(); };
        SourceInitialized += (_, _) => UseDarkTitleBar();
        try
        {
            using var ico = typeof(SettingsWindow).Assembly.GetManifestResourceStream("Assets.app.ico");
            if (ico is not null) Icon = BitmapFrame.Create(ico, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch
        {
            // bez ikony
        }
        // Na menších obrazovkách (nebo při zvětšení 125 %+) okno zmenšit, aby se vešlo.
        var work = SystemParameters.WorkArea;
        Height = Math.Min(Height, work.Height - 24);
        Width = Math.Min(Width, work.Width - 24);

        UpdateControllers(Array.Empty<ControllerStatus>());
    }

    // ------------------------------------------------------------------ nastavení

    private void LoadFromSettings()
    {
        _loading = true;
        Vibration.IsChecked = _settings.VibrationEnabled;
        TriggerRumble.IsChecked = _settings.TriggerRumble;
        TriggerRumbleOnlyPressed.IsChecked = _settings.TriggerRumbleOnlyPressed;
        LeftTrigger.Setting = _settings.LeftTrigger;
        RightTrigger.Setting = _settings.RightTrigger;
        LightbarOn.IsChecked = _settings.LightbarEnabled;
        Brightness.Value = _settings.LightbarBrightness;
        var (h, s, _) = ColorUtil.ToHsv(_settings.LightbarColor);
        Hue.Value = h;
        Saturation.Value = s * 100;
        LowBattery.IsChecked = _settings.LowBatteryLightbar;
        PlayerLeds.IsChecked = _settings.PlayerLeds;
        Touchpad.IsChecked = _settings.TouchpadAsBack;
        Notifications.IsChecked = _settings.Notifications;
        _loading = false;
        SyncDependentUi();
    }

    /// <summary>Znovu načíst hodnoty (např. když se vibrace přepnuly z menu v liště).</summary>
    public void RefreshFromSettings() => LoadFromSettings();

    private void Commit()
    {
        if (_loading) return;
        _settings.VibrationEnabled = Vibration.IsChecked == true;
        _settings.TriggerRumble = TriggerRumble.IsChecked == true;
        _settings.TriggerRumbleOnlyPressed = TriggerRumbleOnlyPressed.IsChecked == true;
        _settings.LeftTrigger = LeftTrigger.Setting;
        _settings.RightTrigger = RightTrigger.Setting;
        _settings.LightbarEnabled = LightbarOn.IsChecked == true;
        _settings.LightbarBrightness = (int)Brightness.Value;
        _settings.LowBatteryLightbar = LowBattery.IsChecked == true;
        _settings.PlayerLeds = PlayerLeds.IsChecked == true;
        _settings.TouchpadAsBack = Touchpad.IsChecked == true;
        _settings.Notifications = Notifications.IsChecked == true;
        SyncDependentUi();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Barva z posuvníků odstínu a sytosti – mění se hned při posouvání.</summary>
    private void ColorFromSliders()
    {
        if (_loading) return;
        _settings.LightbarColor = ColorUtil.FromHsv(Hue.Value, Saturation.Value / 100, 1);
        Commit();
    }

    private void SelectSwatch(int rgb)
    {
        var (h, s, _) = ColorUtil.ToHsv(rgb);
        _loading = true;
        // Bílá nemá odstín – ponechat ten současný.
        if (s > 0.01) Hue.Value = h;
        Saturation.Value = s * 100;
        _loading = false;
        _settings.LightbarColor = rgb;
        Commit();
    }

    private void SyncDependentUi()
    {
        TriggerRumbleOnlyPressed.IsEnabled = TriggerRumble.IsChecked == true;

        bool on = LightbarOn.IsChecked == true;
        LightbarControls.IsEnabled = on;
        LightbarControls.Opacity = on ? 1 : 0.4;

        var full = ToMedia(ColorUtil.FromHsv(Hue.Value, 1, 1));
        Saturation.Background = new LinearGradientBrush(Colors.White, full, 0);

        var c = ToMedia(_settings.LightbarColor);
        double alpha = on ? 0.25 + 0.75 * Brightness.Value / 100 : 0;
        ColorPreview.Background = new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), c.R, c.G, c.B));

        foreach (var sw in Swatches.Children.OfType<Button>())
            sw.Tag = (int)sw.DataContext == _settings.LightbarColor ? "selected" : null;

        _painter.TouchpadAsBack = _settings.TouchpadAsBack;
        int previewAlpha = _settings.LightbarEnabled ? 60 + 195 * _settings.LightbarBrightness / 100 : 0;
        _painter.Lightbar = SD.Color.FromArgb(previewAlpha, SD.Color.FromArgb(_settings.LightbarColor | unchecked((int)0xFF000000)));
        _lastFrameKey = null;
    }

    private void BuildSwatches()
    {
        foreach (int rgb in SwatchColors)
        {
            var b = new Button
            {
                Style = (Style)FindResource("Swatch"),
                Background = new SolidColorBrush(ToMedia(rgb)),
                DataContext = rgb,
                ToolTip = $"#{rgb:X6}",
            };
            b.Click += (_, _) => SelectSwatch(rgb);
            Swatches.Children.Add(b);
        }
    }

    private static Color ToMedia(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    // ------------------------------------------------------------------ ovladače

    public void UpdateControllers(IReadOnlyList<ControllerStatus> controllers)
    {
        _controllers = controllers;
        if (_selectedId is null || controllers.All(c => c.Id != _selectedId))
            _selectedId = controllers.FirstOrDefault()?.Id;

        // Stavové „čipy“ v záhlaví.
        StatusChips.Children.Clear();
        if (controllers.Count == 0)
            StatusChips.Children.Add(Chip(new TextBlock { Text = Loc.T("tray_noController"), Foreground = (Brush)FindResource("TextDim") }));
        foreach (var c in controllers)
            StatusChips.Children.Add(StatusChip(c));

        // Výběr ovladače pro test – jen když jich je víc.
        ControllerPicker.Children.Clear();
        if (controllers.Count > 1)
        {
            string group = "picker";
            foreach (var c in controllers)
            {
                var rb = new RadioButton
                {
                    Style = (Style)FindResource("Segment"),
                    GroupName = group,
                    Content = c.PlayerIndex >= 0 ? Loc.F("sw_player", c.PlayerIndex + 1) : c.Name,
                    IsChecked = c.Id == _selectedId,
                };
                string id = c.Id;
                rb.Checked += (_, _) => { _selectedId = id; _lastFrameKey = null; UpdateLiveView(); };
                ControllerPicker.Children.Add(rb);
            }
        }
        _lastFrameKey = null;
        UpdateLiveView();
    }

    private Border Chip(UIElement content) => new()
    {
        Background = (Brush)FindResource("CardBg"),
        BorderBrush = (Brush)FindResource("CardBorder"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(16),
        Padding = new Thickness(14, 7, 14, 7),
        Margin = new Thickness(8, 0, 0, 0),
        Child = content,
    };

    private Border StatusChip(ControllerStatus c)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        string player = c.PlayerIndex >= 0 ? " · " + Loc.F("sw_player", c.PlayerIndex + 1) : "";
        panel.Children.Add(new TextBlock
        {
            Text = $"{c.Name}{player} · {(c.Connection == ConnectionType.Bluetooth ? "Bluetooth" : "USB")}",
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });

        // Malá baterie.
        int pct = c.BatteryPercent ?? 0;
        var col = TrayIconRenderer.BatteryColor(c.BatteryPercent);
        var battery = new Grid { Width = 30, Height = 14, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        battery.Children.Add(new Border
        {
            BorderBrush = (Brush)FindResource("TextDim"), BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(3),
        });
        battery.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(col.R, col.G, col.B)),
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = Math.Max(1, 24 * pct / 100.0),
        });
        panel.Children.Add(battery);
        panel.Children.Add(new Rectangle { Width = 2, Height = 6, Fill = (Brush)FindResource("TextDim"), VerticalAlignment = VerticalAlignment.Center });

        string text = c.BatteryPercent is null ? "…" : $"{c.BatteryPercent} %";
        if (c.Charging == ChargingState.Charging) text += " ⚡";
        if (c.Charging == ChargingState.Full) text += " · " + Loc.T("sw_charged");
        panel.Children.Add(new TextBlock { Text = text, Foreground = (Brush)FindResource("Text"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return Chip(panel);
    }

    // ------------------------------------------------------------------ živý obrázek ovladače

    private void UpdateLiveView()
    {
        var c = _controllers.FirstOrDefault(x => x.Id == _selectedId);
        _painter.State = c is null ? null : StateProvider?.Invoke(c.Id);
        _painter.PlayerIndex = c is not null && _settings.PlayerLeds ? c.PlayerIndex : -1;

        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)(ImageHost.ActualWidth * dpi.DpiScaleX), h = (int)(ImageHost.ActualHeight * dpi.DpiScaleY);
        if (w < 10 || h < 10) return;

        string key = $"{Loc.Language}|{_painter.State}|{_painter.PlayerIndex}|{_painter.Lightbar.ToArgb()}|{_painter.TouchpadAsBack}|{w}x{h}";
        if (key == _lastFrameKey) return;
        _lastFrameKey = key;

        using var bmp = _painter.Render(w, h);
        if (_frame is null || _frame.PixelWidth != w || _frame.PixelHeight != h)
        {
            _frame = new WriteableBitmap(w, h, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32, null);
            ControllerImage.Source = _frame;
        }
        var data = bmp.LockBits(new SD.Rectangle(0, 0, w, h), SD.Imaging.ImageLockMode.ReadOnly, SD.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            _frame.WritePixels(new Int32Rect(0, 0, w, h), data.Scan0, data.Stride * h, data.Stride);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    // ------------------------------------------------------------------ okno

    private void SyncLanguageButtons()
    {
        LangCs.IsChecked = Loc.Language == Loc.Czech;
        LangEn.IsChecked = Loc.Language == Loc.English;
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>Opravdu zavřít (při ukončení aplikace); jinak křížek okno jen schová.</summary>
    public void CloseForReal()
    {
        _allowClose = true;
        _liveTimer.Stop();
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke(this, EventArgs.Empty);
            return;
        }
        base.OnClosing(e);
    }

    private void UseDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int on = 1;
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
            int caption = 0x00221D1B; // COLORREF (BGR) = #1B1D22
            DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR (Windows 11)
        }
        catch
        {
            // Starší Windows – zůstane výchozí titulek.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
