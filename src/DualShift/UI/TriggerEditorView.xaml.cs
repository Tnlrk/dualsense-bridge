using DualShift.Localization;
using System.Windows;
using System.Windows.Controls;
using DualShift.DualSense;

namespace DualShift.UI;

/// <summary>Nastavení jednoho adaptivního triggeru: typ efektu (segmenty) + posuvníky.</summary>
public partial class TriggerEditorView : UserControl
{
    private bool _loading;

    private static readonly Dictionary<TriggerEffectKind, string> HintKeys = new()
    {
        [TriggerEffectKind.Off] = "tr_hint_off",
        [TriggerEffectKind.Resistance] = "tr_hint_resistance",
        [TriggerEffectKind.Weapon] = "tr_hint_weapon",
        [TriggerEffectKind.Vibration] = "tr_hint_vibration",
    };

    public event EventHandler? EffectChanged;

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(TriggerEditorView),
        new PropertyMetadata("", (d, e) => ((TriggerEditorView)d).Title.Text = (string)e.NewValue));

    public TriggerEditorView()
    {
        InitializeComponent();
        Loc.LanguageChanged += () => Hint.Text = Loc.T(HintKeys[Kind]);
        string group = Guid.NewGuid().ToString("N");
        foreach (var rb in Radios) rb.GroupName = group;

        foreach (var rb in Radios) rb.Checked += (_, _) => { UpdateVisibility(); Changed(); };
        foreach (var s in new[] { Start, End, Strength, Frequency })
            s.ValueChanged += (_, _) => Changed();

        Setting = TriggerEffect.None;
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    private RadioButton[] Radios => new[] { KindOff, KindResistance, KindWeapon, KindVibration };

    private TriggerEffectKind Kind =>
        KindResistance.IsChecked == true ? TriggerEffectKind.Resistance :
        KindWeapon.IsChecked == true ? TriggerEffectKind.Weapon :
        KindVibration.IsChecked == true ? TriggerEffectKind.Vibration :
        TriggerEffectKind.Off;

    public TriggerEffect Setting
    {
        get => new()
        {
            Kind = Kind,
            StartPosition = (int)Start.Value,
            EndPosition = (int)End.Value,
            Strength = (int)Strength.Value,
            Frequency = (int)Frequency.Value,
        };
        set
        {
            _loading = true;
            (value.Kind switch
            {
                TriggerEffectKind.Resistance => KindResistance,
                TriggerEffectKind.Weapon => KindWeapon,
                TriggerEffectKind.Vibration => KindVibration,
                _ => KindOff,
            }).IsChecked = true;
            UpdateVisibility();
            Start.Value = value.StartPosition;
            End.Value = value.EndPosition;
            Strength.Value = value.Strength;
            Frequency.Value = value.Frequency;
            _loading = false;
        }
    }

    private void UpdateVisibility()
    {
        var k = Kind;
        Hint.Text = Loc.T(HintKeys[k]);
        bool on = k != TriggerEffectKind.Off;
        StartRow.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        StrengthRow.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        EndRow.Visibility = k == TriggerEffectKind.Weapon ? Visibility.Visible : Visibility.Collapsed;
        FrequencyRow.Visibility = k == TriggerEffectKind.Vibration ? Visibility.Visible : Visibility.Collapsed;

        // Spoušť má začátek 2–7, ostatní efekty 0–9.
        Start.Minimum = k == TriggerEffectKind.Weapon ? 2 : 0;
        Start.Maximum = k == TriggerEffectKind.Weapon ? 7 : 9;
    }

    private void Changed()
    {
        if (_loading) return;
        // Cvaknutí musí být za začátkem.
        if (Kind == TriggerEffectKind.Weapon && End.Value <= Start.Value)
        {
            _loading = true;
            End.Value = Math.Min(8, Start.Value + 1);
            _loading = false;
        }
        EffectChanged?.Invoke(this, EventArgs.Empty);
    }
}
