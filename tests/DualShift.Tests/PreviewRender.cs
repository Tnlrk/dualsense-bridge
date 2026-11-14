using System.IO;
using System.Drawing;
using DualShift.Bridge;
using DualShift.DualSense;
using DualShift.UI;

namespace DualShift.Tests;

/// <summary>
/// Vykreslí náhledy UI do PNG (jen když je nastavená proměnná PREVIEW_DIR) – kontrola vzhledu bez spuštění aplikace.
/// </summary>
[Collection("Loc")]
public class PreviewRender
{
    private static readonly DualSenseState Pressed = new(30, 200, 220, 128, 90, 255,
        DsButtons.Cross | DsButtons.Triangle | DsButtons.R1 | DsButtons.R2 | DsButtons.DpadLeft | DsButtons.Options | DsButtons.L3,
        70, ChargingState.Discharging);

    [Fact]
    public void RenderPreview()
    {
        var dir = Environment.GetEnvironmentVariable("PREVIEW_DIR");
        if (string.IsNullOrEmpty(dir)) return;

        RenderPainter(Path.Combine(dir, "pressed.png"), Pressed);
        RenderPainter(Path.Combine(dir, "none.png"), null);

        var t = new Thread(() =>
        {
            var before = DualShift.Localization.Loc.Language;
            try
            {
                DualShift.Localization.Loc.SetLanguage("cs");
                RenderWindow(Path.Combine(dir, "window.png"));
                DualShift.Localization.Loc.SetLanguage("en");
                RenderWindow(Path.Combine(dir, "window_en.png"));
                RenderHelp(Path.Combine(dir, "help_en.png"));
                DualShift.Localization.Loc.SetLanguage("cs");
                RenderHelp(Path.Combine(dir, "help.png"));
            }
            finally
            {
                DualShift.Localization.Loc.SetLanguage(before);
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
    }

    private static void RenderHelp(string path)
    {
        var w = new HelpWindow
        {
            WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
            Left = -5000,
            Top = -5000,
            ShowInTaskbar = false,
            Height = 2600,
        };
        w.Show();
        Pump(w);
        Snapshot(w, path);
        w.CloseForReal();
    }

    private static void Snapshot(System.Windows.Window w, string path)
    {
        var content = (System.Windows.FrameworkElement)w.Content;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(w);
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)(content.ActualWidth * dpi.DpiScaleX), (int)(content.ActualHeight * dpi.DpiScaleY),
            96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, System.Windows.Media.PixelFormats.Pbgra32);
        var bg = new System.Windows.Media.DrawingVisual();
        using (var dc = bg.RenderOpen())
            dc.DrawRectangle(w.Background, null, new System.Windows.Rect(0, 0, content.ActualWidth, content.ActualHeight));
        rtb.Render(bg);
        rtb.Render(content);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    private static void RenderPainter(string path, DualSenseState? s)
    {
        var p = new ControllerPainter { State = s, PlayerIndex = 0 };
        p.Render(760, 572).Save(path);
    }

    private static void RenderWindow(string path)
    {
        var settings = new AppSettings
        {
            LightbarColor = 0x8A2BFF,
            LightbarBrightness = 70,
            TriggerRumble = true,
            RightTrigger = new TriggerEffect { Kind = TriggerEffectKind.Weapon, StartPosition = 3, EndPosition = 6, Strength = 6 },
            LeftTrigger = new TriggerEffect { Kind = TriggerEffectKind.Resistance, StartPosition = 2, Strength = 4 },
        };
        var w = new SettingsWindow(settings)
        {
            WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
            Left = -5000,
            Top = -5000,
            ShowInTaskbar = false,
            Height = 1300,
            StateProvider = _ => Pressed,
        };
        w.Show();
        w.UpdateControllers(new[]
        {
            new ControllerStatus("a", "DualSense", ConnectionType.Bluetooth, 75, ChargingState.Charging, 0),
        });
        Pump(w);

        var content = (System.Windows.FrameworkElement)w.Content;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(w);
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)(content.ActualWidth * dpi.DpiScaleX), (int)(content.ActualHeight * dpi.DpiScaleY),
            96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, System.Windows.Media.PixelFormats.Pbgra32);
        var bg = new System.Windows.Media.DrawingVisual();
        using (var dc = bg.RenderOpen())
            dc.DrawRectangle(w.Background, null, new System.Windows.Rect(0, 0, content.ActualWidth, content.ActualHeight));
        rtb.Render(bg);
        rtb.Render(content);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
        using (var fs = File.Create(path)) enc.Save(fs);
        w.CloseForReal();
    }

    private static void Pump(System.Windows.Window w)
    {
        for (int i = 0; i < 10; i++)
        {
            w.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Thread.Sleep(50);
        }
    }
}
