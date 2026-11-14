using DualShift.Localization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;
using DualShift.Bridge;
using DualShift.DualSense;

namespace DualShift.UI;

/// <summary>
/// Kreslí fotku ovladače s překryvy: stisknutá tlačítka se rozsvítí, páčky a triggery ukazují polohu
/// a u každého tlačítka je štítek s Xbox tlačítkem, na které je namapované. Tmavé téma.
/// </summary>
public sealed class ControllerPainter
{
    // Logické plátno: nahoře pruh pro L2/R2 a L1/R1, pod ním fotka 1200×803 (Assets/dualsense.png, průhledné pozadí).
    public const float W = 1200, ImgH = 803, TopBand = 100, H = TopBand + ImgH;

    private static readonly Color Accent = Color.FromArgb(0x4C, 0x9A, 0xFF);
    private static readonly Color Idle = Color.FromArgb(0xC9, 0xCE, 0xD8);
    private static readonly Color Chip = Color.FromArgb(0x3A, 0x3F, 0x4A);
    private static readonly Color TextMain = Color.FromArgb(0xEE, 0xF0, 0xF3);
    private static readonly Color TextDim = Color.FromArgb(0x9A, 0xA1, 0xAD);

    private static readonly Lazy<Image?> Photo = new(() =>
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Assets.dualsense.png");
            if (s is null) return null;
            using var tmp = Image.FromStream(s);
            return new Bitmap(tmp);
        }
        catch
        {
            return null;
        }
    });

    public DualSenseState? State { get; set; }
    public bool TouchpadAsBack { get; set; } = true;
    /// <summary>Barva lišty; průhlednost = jas, A = 0 → vypnutá.</summary>
    public Color Lightbar { get; set; } = Color.FromArgb(0x00, 0x40, 0xFF);
    public int PlayerIndex { get; set; } = -1;
    public Color Background { get; set; } = Color.FromArgb(0x24, 0x27, 0x2E);

    private bool Pressed(DsButtons b) => State is { } s && (s.Buttons & b) != 0;
    private string? Xbox(DsButtons b) => XInputMapper.XboxLabel(b, TouchpadAsBack);

    /// <summary>Bod na fotce (souřadnice 1200×803) → plátno.</summary>
    private static PointF P(float x, float y) => new(x, TopBand + y);

    // Fotka zmenšená na aktuální velikost se počítá jen jednou (kvalitní zmenšení je drahé);
    // každý snímek se pak jen zkopíruje a dokreslí se překryvy.
    private Bitmap? _backdrop, _frame;
    private Color _backdropColor;

    /// <summary>
    /// Vykreslí snímek dané velikosti (v pixelech). Vrácená bitmapa patří painteru
    /// a příštím voláním se přepíše – nevolat na ni Dispose.
    /// </summary>
    public Bitmap Render(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (_frame is null || _frame.Width != width || _frame.Height != height || _backdropColor != Background)
        {
            _frame?.Dispose();
            _backdrop?.Dispose();
            _frame = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            _backdrop = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            _backdropColor = Background;
            using var bg = Graphics.FromImage(_backdrop);
            bg.Clear(Background);
            if (Photo.Value is { } img)
            {
                bg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                bg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                ApplyTransform(bg, width, height);
                bg.DrawImage(img, 0, TopBand, W, ImgH);
            }
        }

        using var g = Graphics.FromImage(_frame);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImageUnscaled(_backdrop!, 0, 0);
        g.CompositingMode = CompositingMode.SourceOver;
        PaintOverlays(g, width, height);
        return _frame;
    }

    private static void ApplyTransform(Graphics g, float width, float height)
    {
        float scale = Math.Min(width / W, height / H);
        g.TranslateTransform((width - W * scale) / 2, (height - H * scale) / 2);
        g.ScaleTransform(scale, scale);
    }

    private void PaintOverlays(Graphics g, float width, float height)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        ApplyTransform(g, width, height);

        DrawShoulders(g);
        DrawLightbarAndLeds(g);
        DrawTouchpad(g);
        DrawDpad(g);
        DrawFaceButtons(g);
        DrawSticks(g);
        DrawCenterButtons(g);
        DrawInfo(g);

        if (State is null)
        {
            using var veil = new SolidBrush(Color.FromArgb(185, Background));
            g.FillRectangle(veil, -W, -H, 3 * W, 3 * H);
            DrawText(g, Loc.T("view_noController"), P(600, 600), 44, TextMain, bold: true);
            DrawText(g, Loc.T("view_connectHint"), P(600, 660), 26, TextDim);
        }
    }

    // ------------------------------------------------------------------ zvýraznění

    private static void Highlight(Graphics g, GraphicsPath path, bool on)
    {
        if (!on) return;
        using var fill = new SolidBrush(Color.FromArgb(120, Accent));
        g.FillPath(fill, path);
        using var pen = new Pen(Accent, 5) { LineJoin = LineJoin.Round };
        g.DrawPath(pen, path);
    }

    private static GraphicsPath Circle(PointF c, float r)
    {
        var p = new GraphicsPath();
        p.AddEllipse(c.X - r, c.Y - r, 2 * r, 2 * r);
        return p;
    }

    private static GraphicsPath Box(float x, float y, float w, float h, float radius)
    {
        var o = P(x, y);
        return RoundedRect(new RectangleF(o.X, o.Y, w, h), radius);
    }

    // ------------------------------------------------------------------ L1/L2/R1/R2

    private void DrawShoulders(Graphics g)
    {
        DrawTriggerBar(g, new RectangleF(150, 6, 150, 42), "L2", State?.L2 ?? 0, DsButtons.L2, new PointF(105, 27));
        DrawTriggerBar(g, new RectangleF(905, 6, 150, 42), "R2", State?.R2 ?? 0, DsButtons.R2, new PointF(1100, 27));
        DrawBumper(g, new RectangleF(155, 56, 140, 32), "L1", DsButtons.L1, new PointF(105, 72));
        DrawBumper(g, new RectangleF(910, 56, 140, 32), "R1", DsButtons.R1, new PointF(1100, 72));
    }

    private void DrawTriggerBar(Graphics g, RectangleF r, string name, byte value, DsButtons b, PointF badge)
    {
        using var path = RoundedRect(r, 14);
        using var bg = new SolidBrush(Chip);
        g.FillPath(bg, path);
        if (value > 0)
        {
            var st = g.Save();
            g.SetClip(path);
            using var fill = new SolidBrush(Accent);
            g.FillRectangle(fill, r.X, r.Y, r.Width * value / 255f, r.Height);
            g.Restore(st);
        }
        DrawText(g, $"{name}  {value * 100 / 255} %", new PointF(r.X + r.Width / 2, r.Y + r.Height / 2), 20,
            Color.White, bold: true);
        DrawBadge(g, badge, Xbox(b), value > 0);
    }

    private void DrawBumper(Graphics g, RectangleF r, string name, DsButtons b, PointF badge)
    {
        bool on = Pressed(b);
        using var path = RoundedRect(r, 12);
        using var bg = new SolidBrush(on ? Accent : Chip);
        g.FillPath(bg, path);
        DrawText(g, name, new PointF(r.X + r.Width / 2, r.Y + r.Height / 2), 19, on ? Color.White : Idle, bold: true);
        DrawBadge(g, badge, Xbox(b), on);
    }

    // ------------------------------------------------------------------ světelná lišta a LED

    private void DrawLightbarAndLeds(Graphics g)
    {
        if (Lightbar.A > 0)
        {
            using var glow = new Pen(Color.FromArgb(110 * Lightbar.A / 255, Lightbar), 18) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var bar = new Pen(Lightbar, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            foreach (var (a, b) in new[] { (P(363, 40), P(392, 255)), (P(857, 40), P(828, 255)) })
            {
                g.DrawLine(glow, a, b);
                g.DrawLine(bar, a, b);
            }
        }

        byte leds = OutputReportBuilder.PlayerLedsFor(PlayerIndex);
        for (int i = 0; i < 5; i++)
        {
            if ((leds & (1 << i)) == 0) continue;
            var c = P(605 + (i - 2) * 18, 287);
            using var halo = new SolidBrush(Color.FromArgb(110, Color.White));
            g.FillEllipse(halo, c.X - 9, c.Y - 9, 18, 18);
            g.FillEllipse(Brushes.White, c.X - 4.5f, c.Y - 4.5f, 9, 9);
        }
    }

    // ------------------------------------------------------------------ touchpad

    private void DrawTouchpad(Graphics g)
    {
        bool on = Pressed(DsButtons.Touchpad);
        using var path = new GraphicsPath();
        path.AddPolygon(new[] { P(372, 22), P(850, 22), P(820, 268), P(402, 268) });
        Highlight(g, path, on);
        DrawBadge(g, P(612, 150), Xbox(DsButtons.Touchpad), on, prefix: "Touchpad");
    }

    // ------------------------------------------------------------------ D-pad

    private void DrawDpad(Graphics g)
    {
        using (var up = Box(190, 128, 76, 88, 18)) Highlight(g, up, Pressed(DsButtons.DpadUp));
        using (var down = Box(190, 245, 76, 88, 18)) Highlight(g, down, Pressed(DsButtons.DpadDown));
        using (var left = Box(120, 193, 90, 76, 18)) Highlight(g, left, Pressed(DsButtons.DpadLeft));
        using (var right = Box(246, 193, 86, 76, 18)) Highlight(g, right, Pressed(DsButtons.DpadRight));

        bool any = Pressed(DsButtons.DpadUp) || Pressed(DsButtons.DpadDown) ||
                   Pressed(DsButtons.DpadLeft) || Pressed(DsButtons.DpadRight);
        DrawBadge(g, P(228, 372), "D-pad", any);
    }

    // ------------------------------------------------------------------ △ ○ ✕ □

    private void DrawFaceButtons(Graphics g)
    {
        DrawFace(g, P(987, 143), DsButtons.Triangle, P(1040, 96));
        DrawFace(g, P(1077, 233), DsButtons.Circle, P(1130, 186));
        DrawFace(g, P(987, 322), DsButtons.Cross, P(1040, 369));
        DrawFace(g, P(898, 233), DsButtons.Square, P(850, 290));
    }

    private void DrawFace(Graphics g, PointF c, DsButtons b, PointF badge)
    {
        bool on = Pressed(b);
        using var path = Circle(c, 42);
        Highlight(g, path, on);
        DrawBadge(g, badge, Xbox(b), on);
    }

    // ------------------------------------------------------------------ páčky

    private void DrawSticks(Graphics g)
    {
        var s = State;
        DrawStick(g, P(410, 392), s?.LeftX ?? 128, s?.LeftY ?? 128, DsButtons.L3, P(410, 505));
        DrawStick(g, P(803, 392), s?.RightX ?? 128, s?.RightY ?? 128, DsButtons.R3, P(803, 505));
    }

    private void DrawStick(Graphics g, PointF c, byte x, byte y, DsButtons b, PointF badge)
    {
        const float travel = 48, knob = 20;
        bool on = Pressed(b);
        if (on)
        {
            using var ring = new Pen(Accent, 8);
            g.DrawEllipse(ring, c.X - 84, c.Y - 84, 168, 168);
        }

        float dx = (x - 127.5f) / 127.5f * travel, dy = (y - 127.5f) / 127.5f * travel;
        bool moved = Math.Abs(x - 128) > 12 || Math.Abs(y - 128) > 12;
        var k = new PointF(c.X + dx, c.Y + dy);
        if (moved)
        {
            using var line = new Pen(Color.FromArgb(200, Accent), 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(line, c, k);
        }
        using var dot = new SolidBrush(moved || on ? Accent : Color.FromArgb(150, 0xB0, 0xB6, 0xC0));
        g.FillEllipse(dot, k.X - knob, k.Y - knob, 2 * knob, 2 * knob);
        using var rim = new Pen(Color.White, 3);
        g.DrawEllipse(rim, k.X - knob, k.Y - knob, 2 * knob, 2 * knob);

        DrawBadge(g, badge, Xbox(b), on || moved);
    }

    // ------------------------------------------------------------------ Create, Options, PS, Mute

    private void DrawCenterButtons(Graphics g)
    {
        using (var create = Box(306, 70, 36, 58, 17)) Highlight(g, create, Pressed(DsButtons.Create));
        using (var options = Box(875, 70, 36, 58, 17)) Highlight(g, options, Pressed(DsButtons.Options));
        DrawBadge(g, P(262, 80), Xbox(DsButtons.Create), Pressed(DsButtons.Create));
        DrawBadge(g, P(958, 80), Xbox(DsButtons.Options), Pressed(DsButtons.Options));

        using (var ps = Circle(P(605, 390), 40)) Highlight(g, ps, Pressed(DsButtons.PS));
        DrawBadge(g, P(680, 392), Xbox(DsButtons.PS), Pressed(DsButtons.PS));

        using (var mute = Box(570, 455, 70, 22, 11)) Highlight(g, mute, Pressed(DsButtons.Mute));
    }

    // ------------------------------------------------------------------ text mezi rukojeťmi

    private void DrawInfo(Graphics g)
    {
        if (State is not { } s) return;

        var pressed = new List<string>();
        foreach (var (b, name) in ButtonNames)
        {
            if ((s.Buttons & b) == 0) continue;
            var x = Xbox(b);
            string item = x is null ? $"{name} {Loc.T("view_none")}" : x == name ? name : $"{name} → {x}";
            // Nezalamovat uvnitř položky (pevné mezery; GDI+ by jinak lámal i u spojovníku v „D-pad“).
            pressed.Add(item.Replace("D-pad", Loc.T("view_arrow")).Replace(' ', ' '));
        }
        string line1 = pressed.Count == 0 ? Loc.T("view_pressAny") : Loc.T("view_pressed") + string.Join(",  ", pressed);

        var area = new RectangleF(265, TopBand + 575, 670, 125);
        using (var font = new Font("Segoe UI", pressed.Count > 6 ? 27 : 31, pressed.Count > 0 ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(pressed.Count == 0 ? TextDim : TextMain))
        using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord })
            g.DrawString(line1, font, brush, area, fmt);

        DrawTextFit(g, $"{Loc.T("view_leftStick")}  X {Axis(s.LeftX)}  Y {Axis(s.LeftY, true)}     " +
                       $"{Loc.T("view_rightStick")}  X {Axis(s.RightX)}  Y {Axis(s.RightY, true)}", P(600, 722), 25, 660, TextDim);
        DrawTextFit(g, $"LT {s.L2 * 100 / 255} %     RT {s.R2 * 100 / 255} %", P(600, 760), 25, 660, TextDim);
    }

    private static string Axis(byte v, bool invert = false)
    {
        float f = (v - 127.5f) / 127.5f;
        if (invert) f = -f;
        return f.ToString("+0.00;-0.00; 0.00");
    }

    private static readonly (DsButtons, string)[] ButtonNames =
    {
        (DsButtons.Cross, "✕"), (DsButtons.Circle, "○"), (DsButtons.Square, "□"), (DsButtons.Triangle, "△"),
        (DsButtons.L1, "L1"), (DsButtons.R1, "R1"), (DsButtons.L2, "L2"), (DsButtons.R2, "R2"),
        (DsButtons.L3, "L3"), (DsButtons.R3, "R3"),
        (DsButtons.DpadUp, "D-pad ↑"), (DsButtons.DpadDown, "D-pad ↓"), (DsButtons.DpadLeft, "D-pad ←"), (DsButtons.DpadRight, "D-pad →"),
        (DsButtons.Create, "Create"), (DsButtons.Options, "Options"), (DsButtons.PS, "PS"),
        (DsButtons.Touchpad, "Touchpad"), (DsButtons.Mute, "Mute"),
    };

    // ------------------------------------------------------------------ pomocné kreslení

    /// <summary>Štítek s Xbox tlačítkem. A/B/X/Y v barvách Xboxu.</summary>
    private static void DrawBadge(Graphics g, PointF center, string? label, bool active, string? prefix = null)
    {
        label ??= "—";
        var color = label switch
        {
            "A" => Color.FromArgb(0x2E, 0x9E, 0x3A),
            "B" => Color.FromArgb(0xD8, 0x2F, 0x31),
            "X" => Color.FromArgb(0x23, 0x6E, 0xDB),
            "Y" => Color.FromArgb(0xE0, 0xA8, 0x10),
            "—" => Color.FromArgb(0x6A, 0x70, 0x7A),
            _ => Color.FromArgb(0x3C, 0x42, 0x4D),
        };
        string text = prefix is null ? label : $"{prefix} → {label}";
        if (!active) color = Color.FromArgb(200, color);

        using var font = new Font("Segoe UI", 23, FontStyle.Bold, GraphicsUnit.Pixel);
        var size = g.MeasureString(text, font);
        bool round = text.Length == 1;
        float w = round ? 44 : size.Width + 16, h = round ? 44 : 36;
        var r = new RectangleF(center.X - w / 2, center.Y - h / 2, w, h);

        if (active)
        {
            using var glow = new SolidBrush(Color.FromArgb(110, Accent));
            var gr = RectangleF.Inflate(r, 6, 6);
            if (round) g.FillEllipse(glow, gr);
            else using (var gp = RoundedRect(gr, 22)) g.FillPath(glow, gp);
        }

        using var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
        var sr = r;
        sr.Offset(0, 2);
        using var fill = new SolidBrush(color);
        if (round)
        {
            g.FillEllipse(shadow, sr);
            g.FillEllipse(fill, r);
        }
        else
        {
            using (var sp = RoundedRect(sr, 16)) g.FillPath(shadow, sp);
            using (var gp = RoundedRect(r, 16)) g.FillPath(fill, gp);
        }

        DrawText(g, text, center, 23, Color.White, bold: true);
    }

    private static void DrawText(Graphics g, string text, PointF center, float px, Color color, bool bold = false)
    {
        using var font = new Font("Segoe UI", px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, center, fmt);
    }

    /// <summary>Text zmenšený tak, aby se vešel do šířky (mezi rukojeti ovladače).</summary>
    private static void DrawTextFit(Graphics g, string text, PointF center, float px, float maxWidth, Color color, bool bold = false)
    {
        for (; px > 12; px -= 1)
        {
            using var f = new Font("Segoe UI", px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            if (g.MeasureString(text, f).Width <= maxWidth) break;
        }
        DrawText(g, text, center, px, color, bold);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
