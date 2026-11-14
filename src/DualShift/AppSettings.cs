using System.Text.Json;
using System.Text.Json.Serialization;
using DualShift.DualSense;

namespace DualShift;

/// <summary>Nastavení aplikace, ukládá se do %AppData%\DualShift\settings.json.</summary>
public sealed class AppSettings
{
    // Vibrace
    public bool VibrationEnabled { get; set; } = true;
    public bool TriggerRumble { get; set; }                 // vibrace triggerů podle hry
    public bool TriggerRumbleOnlyPressed { get; set; } = true; // jen zmáčknutý trigger (brzda/plyn)

    // Adaptivní triggery
    public TriggerEffect LeftTrigger { get; set; } = TriggerEffect.None;
    public TriggerEffect RightTrigger { get; set; } = TriggerEffect.None;

    // LED
    public bool LightbarEnabled { get; set; } = true;
    public int LightbarColor { get; set; } = 0x0040FF;      // RGB
    public int LightbarBrightness { get; set; } = 100;      // 1–100 %
    public bool LowBatteryLightbar { get; set; } = true;
    public bool PlayerLeds { get; set; } = true;

    // Ovládání
    public bool TouchpadAsBack { get; set; } = true;

    // Jazyk: "cs" / "en"; null = podle Windows
    public string? Language { get; set; }

    // Upozornění
    public bool Notifications { get; set; } = true;

    // HidHide
    public bool HideOriginal { get; set; }
    public List<string> HiddenInstanceIds { get; set; } = new();
    public bool HidHideOffered { get; set; }

    // Upozornění „aplikace běží dál v liště“ už bylo zobrazeno.
    public bool TrayHintShown { get; set; }

    /// <summary>Ručně upravený nebo poškozený soubor nesmí aplikaci shodit – doplní chybějící a ořízne rozsahy.</summary>
    private AppSettings Sanitized()
    {
        LeftTrigger ??= TriggerEffect.None;
        RightTrigger ??= TriggerEffect.None;
        HiddenInstanceIds ??= new();
        LightbarColor &= 0xFFFFFF;
        LightbarBrightness = Math.Clamp(LightbarBrightness, 1, 100);
        return this;
    }

    public AppSettings Clone()
    {
        var c = (AppSettings)MemberwiseClone();
        c.HiddenInstanceIds = new List<string>(HiddenInstanceIds);
        return c;
    }

    // ---- Uložení ----

    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DualShift");

    private static string FilePath => Path.Combine(Directory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Složka nastavení z doby, kdy se aplikace jmenovala „DualSense Bridge“.</summary>
    private static string LegacyFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DualSenseBridge", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            // Převzít nastavení ze staré verze (jednorázově).
            if (!File.Exists(FilePath) && File.Exists(LegacyFilePath))
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.Copy(LegacyFilePath, FilePath);
            }
            if (File.Exists(FilePath))
                return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new()).Sanitized();
        }
        catch
        {
            // Poškozený soubor – začít s výchozím nastavením.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch
        {
            // Nastavení není kritické – při chybě zápisu jen pokračovat.
        }
    }
}
