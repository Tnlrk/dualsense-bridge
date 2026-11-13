using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DualShift.Localization;

namespace DualShift.UI;

/// <summary>Okno nápovědy – obsah z <see cref="HelpContent"/>, přepíná se s jazykem aplikace.</summary>
public partial class HelpWindow : Window
{
    private bool _allowClose;

    public HelpWindow()
    {
        InitializeComponent();
        var work = SystemParameters.WorkArea;
        Height = Math.Min(Height, work.Height - 24);
        SourceInitialized += (_, _) => DarkTitleBar.Apply(this);
        try
        {
            using var ico = typeof(HelpWindow).Assembly.GetManifestResourceStream("Assets.app.ico");
            if (ico is not null) Icon = BitmapFrame.Create(ico, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch
        {
            // bez ikony
        }

        Build();
        Loc.LanguageChanged += Build;
    }

    private void Build()
    {
        Toc.Children.Clear();
        Sections.Children.Clear();

        foreach (var section in HelpContent.For(Loc.Language))
        {
            var card = new Border { Style = (Style)FindResource("Card") };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = section.Title, Style = (Style)FindResource("H2") });
            foreach (var block in section.Blocks)
                panel.Children.Add(Render(block));
            card.Child = panel;
            Sections.Children.Add(card);

            // Odkaz v obsahu – posune na sekci.
            var link = new Button { Content = section.Title, Margin = new Thickness(4), Padding = new Thickness(12, 6, 12, 6) };
            link.Click += (_, _) => card.BringIntoView(new Rect(0, 0, 1, card.ActualHeight));
            Toc.Children.Add(link);
        }
        Scroller.ScrollToTop();
    }

    private UIElement Render(HelpContent.Block block) => block switch
    {
        HelpContent.Text t => new TextBlock
        {
            Text = t.Value,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 2, 0, 8),
            Foreground = (Brush)FindResource("Text"),
        },
        HelpContent.Bullets b => Bullets(b.Items),
        HelpContent.MappingTable m => Table(m),
        _ => new TextBlock(),
    };

    private UIElement Bullets(IEnumerable<string> items)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        foreach (var item in items)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6, Fill = (Brush)FindResource("Accent"),
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2, 7, 0, 0),
            });
            var text = new TextBlock
            {
                Text = item, TextWrapping = TextWrapping.Wrap, LineHeight = 20,
                Foreground = (Brush)FindResource("Text"),
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            panel.Children.Add(row);
        }
        return panel;
    }

    private UIElement Table(HelpContent.MappingTable m)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        void AddRow(int r, string a, string b, bool header)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var bg = new Border
            {
                Background = header ? (Brush)FindResource("ControlBg") : r % 2 == 0 ? new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x35)) : Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
            };
            Grid.SetRow(bg, r);
            Grid.SetColumnSpan(bg, 2);
            grid.Children.Add(bg);
            foreach (var (text, col) in new[] { (a, 0), (b, 1) })
            {
                var t = new TextBlock
                {
                    Text = text,
                    Margin = new Thickness(12, 7, 12, 7),
                    FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = (Brush)FindResource(header ? "Text" : col == 0 ? "Text" : "TextDim"),
                };
                Grid.SetRow(t, r);
                Grid.SetColumn(t, col);
                grid.Children.Add(t);
            }
        }

        AddRow(0, m.LeftHeader, m.RightHeader, true);
        for (int i = 0; i < m.Rows.Length; i++)
            AddRow(i + 1, m.Rows[i].Ds, m.Rows[i].Xbox, false);
        return grid;
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void CloseForReal()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }
}

/// <summary>Tmavý titulek okna (Windows 10 20H1+ / 11).</summary>
internal static class DarkTitleBar
{
    public static void Apply(Window w)
    {
        try
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            int on = 1;
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
            int caption = 0x00221D1B; // COLORREF (BGR) = #1B1D22
            DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
        }
        catch
        {
            // Starší Windows – výchozí titulek.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
