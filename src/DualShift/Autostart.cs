using Microsoft.Win32;

namespace DualShift;

/// <summary>
/// Spouštění s Windows. Exe se zkopíruje do %LocalAppData%\DualShift, aby šel
/// původní soubor (např. z flashky nebo Stažených souborů) smazat.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DualShift";

    public static string InstalledExePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DualShift", "DualShift.exe");

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Enable()
    {
        string current = Environment.ProcessPath ?? throw new InvalidOperationException("Neznámá cesta k exe.");
        string target = InstalledExePath;
        if (!string.Equals(Path.GetFullPath(current), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(current, target, overwrite: true);
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        // Po přihlášení se spustí jen do lišty, bez okna.
        key.SetValue(ValueName, $"\"{target}\" {Program.MinimizedArg}");
    }

    /// <summary>
    /// Stará verze („DualSense Bridge“) se spouštěla pod jiným názvem z jiné složky.
    /// Pokud měla zapnutý autostart, přepne se na nový název (jinak by se po přihlášení spustila stará verze).
    /// </summary>
    public static void MigrateFromLegacyName()
    {
        const string legacyValue = "DualSenseBridge";
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(legacyValue) is not string) return;
        key.DeleteValue(legacyValue, throwOnMissingValue: false);
        Enable();
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
