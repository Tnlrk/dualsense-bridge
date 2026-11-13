using System.ComponentModel;
using System.Globalization;

namespace DualShift.Localization;

/// <summary>
/// Jednoduchá lokalizace CZ/EN s přepínáním za běhu.
/// V kódu: <c>Loc.T("key")</c> / <c>Loc.F("key", args)</c>.
/// Ve XAML: <c>{Binding [key], Source={x:Static loc:Loc.Instance}}</c> – po změně jazyka se texty obnoví samy.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string Czech = "cs";
    public const string English = "en";

    public static Loc Instance { get; } = new();
    public static string Language { get; private set; } = Czech;

    /// <summary>Volá se po změně jazyka (na vlákně, které jazyk změnilo).</summary>
    public static event Action? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private Loc() { }

    /// <summary>Indexer pro XAML binding.</summary>
    public string this[string key] => T(key);

    public static string T(string key)
    {
        var table = Language == English ? Strings.En : Strings.Cs;
        return table.TryGetValue(key, out var v) ? v : Strings.Cs.GetValueOrDefault(key, key);
    }

    public static string F(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>Výchozí jazyk podle Windows: čeština/slovenština → cs, jinak en.</summary>
    public static string DetectDefault() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "cs" or "sk" ? Czech : English;

    public static string Normalize(string? lang) => lang == English ? English : lang == Czech ? Czech : DetectDefault();

    public static void SetLanguage(string? lang)
    {
        var l = Normalize(lang);
        if (l == Language) return;
        Language = l;
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke();
    }
}
