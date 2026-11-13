using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using DualShift.Bridge;
using DualShift.DualSense;

namespace DualShift.UI;

/// <summary>
/// Dynamicky kreslená ikona do lišty: baterie (barva podle %), blesk při nabíjení,
/// nápis BT/USB. Šedá přeškrtnutá = nepřipojeno, červený vykřičník = chyba.
/// </summary>
public sealed class TrayIconRenderer : IDisposable
{
    private Icon? _current;
    private IntPtr _currentHandle;

    public enum Mode { Connected, Disconnected, Error }

    public Icon Render(Mode mode, ControllerStatus? status)
    {
        var size = SystemInformation.SmallIconSize;
        int s = Math.Max(16, size.Width);
        using var bmp = new Bitmap(s, s);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            switch (mode)
            {
                case Mode.Connected when status is not null:
                    DrawConnected(g, s, status);
                    break;
                case Mode.Error:
                    DrawError(g, s);
                    break;
                default:
                    DrawDisconnected(g, s);
                    break;
            }
        }

        IntPtr h = bmp.GetHicon();
        var icon = Icon.FromHandle(h);
        // Uvolnit předchozí ikonu až po vytvoření nové (NotifyIcon ji ještě může držet).
        ReleaseCurrent();
        _current = icon;
        _currentHandle = h;
        return icon;
    }

    /// <summary>Malý obrázek stavu pro položku menu (volající ho vlastní a uvolní).</summary>
    public Bitmap RenderBitmap(ControllerStatus status)
    {
        int s = Math.Max(16, SystemInformation.SmallIconSize.Width);
        var bmp = new Bitmap(s, s);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);
        DrawConnected(g, s, status, Color.FromArgb(60, 60, 60));
        return bmp;
    }

    private static RectangleF BatteryBody(int s) =>
        new(s * 0.04f, s * 0.08f, s * 0.82f, s * 0.46f);

    private static void DrawBatteryOutline(Graphics g, int s, Color color, out RectangleF inner)
    {
        var body = BatteryBody(s);
        float pw = Math.Max(1f, s / 16f);
        using var pen = new Pen(color, pw);
        g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
        using var nub = new SolidBrush(color);
        g.FillRectangle(nub, body.Right + pw / 2, body.Y + body.Height * 0.28f, s * 0.1f, body.Height * 0.44f);
        float pad = pw * 1.5f;
        inner = new RectangleF(body.X + pad, body.Y + pad, body.Width - 2 * pad, body.Height - 2 * pad);
    }

    public static Color BatteryColor(int? pct) => pct switch
    {
        null => Color.Silver,
        > 50 => Color.FromArgb(0x3C, 0xC8, 0x50),
        >= 20 => Color.FromArgb(0xF5, 0xA6, 0x23),
        _ => Color.FromArgb(0xE5, 0x39, 0x35),
    };

    private static void DrawConnected(Graphics g, int s, ControllerStatus st, Color? outline = null)
    {
        var fg = outline ?? Color.White;
        DrawBatteryOutline(g, s, fg, out var inner);

        int pct = st.BatteryPercent ?? 0;
        if (st.BatteryPercent is not null)
        {
            using var fill = new SolidBrush(BatteryColor(st.BatteryPercent));
            g.FillRectangle(fill, inner.X, inner.Y, Math.Max(1, inner.Width * pct / 100f), inner.Height);
        }

        if (st.Charging is ChargingState.Charging or ChargingState.Full)
            DrawBolt(g, BatteryBody(s));

        // Typ spojení ve spodní polovině.
        string label = st.Connection == ConnectionType.Bluetooth ? "BT" : "USB";
        var area = new RectangleF(0, s * 0.56f, s, s * 0.44f);
        var color = st.Connection == ConnectionType.Bluetooth ? Color.FromArgb(0x4F, 0xA3, 0xFF) : fg;
        DrawFittedText(g, label, area, color);
    }

    private static void DrawBolt(Graphics g, RectangleF r)
    {
        float cx = r.X + r.Width / 2, top = r.Y - r.Height * 0.15f, bottom = r.Bottom + r.Height * 0.15f;
        float w = r.Width * 0.22f, h = bottom - top;
        var pts = new[]
        {
            new PointF(cx + w * 0.35f, top),
            new PointF(cx - w * 0.6f, top + h * 0.56f),
            new PointF(cx - w * 0.02f, top + h * 0.56f),
            new PointF(cx - w * 0.35f, bottom),
            new PointF(cx + w * 0.6f, top + h * 0.42f),
            new PointF(cx + w * 0.02f, top + h * 0.42f),
        };
        using var outline = new Pen(Color.FromArgb(40, 40, 40), Math.Max(1f, r.Height / 8f)) { LineJoin = LineJoin.Round };
        g.DrawPolygon(outline, pts);
        using var fill = new SolidBrush(Color.FromArgb(0xFF, 0xE0, 0x40));
        g.FillPolygon(fill, pts);
    }

    private static void DrawDisconnected(Graphics g, int s)
    {
        var gray = Color.FromArgb(0x9E, 0x9E, 0x9E);
        DrawBatteryOutline(g, s, gray, out _);
        using var pen = new Pen(gray, Math.Max(1.5f, s / 10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, s * 0.15f, s * 0.92f, s * 0.85f, s * 0.12f);
        DrawFittedText(g, "–", new RectangleF(0, s * 0.56f, s * 0.5f, s * 0.44f), gray);
    }

    private static void DrawError(Graphics g, int s)
    {
        using var red = new SolidBrush(Color.FromArgb(0xE5, 0x39, 0x35));
        g.FillEllipse(red, 1, 1, s - 2, s - 2);
        DrawFittedText(g, "!", new RectangleF(0, 0, s, s), Color.White);
    }

    private static void DrawFittedText(Graphics g, string text, RectangleF area, Color color)
    {
        float size = area.Height;
        using var fmt = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        Font? font = null;
        try
        {
            // Zmenšovat písmo, dokud se text nevejde.
            for (; size > 4; size -= 0.5f)
            {
                font?.Dispose();
                font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
                var m = g.MeasureString(text, font, PointF.Empty, fmt);
                if (m.Width <= area.Width && m.Height <= area.Height * 1.1f) break;
            }
            using var brush = new SolidBrush(color);
            using var shadow = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            var sh = area;
            sh.Offset(0.6f, 0.6f);
            g.DrawString(text, font!, shadow, sh, fmt);
            g.DrawString(text, font!, brush, area, fmt);
        }
        finally
        {
            font?.Dispose();
        }
    }

    private void ReleaseCurrent()
    {
        if (_currentHandle != IntPtr.Zero)
        {
            _current?.Dispose();
            DestroyIcon(_currentHandle);
            _currentHandle = IntPtr.Zero;
            _current = null;
        }
    }

    public void Dispose() => ReleaseCurrent();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
