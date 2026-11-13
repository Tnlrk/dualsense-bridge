using Microsoft.Win32;

namespace DualSenseBridge;

/// <summary>
/// Spouštění s Windows. Exe se zkopíruje do %LocalAppData%\DualSenseBridge, aby šel
/// původní soubor (např. z flashky nebo Stažených souborů) smazat.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DualSenseBridge";

    public static string InstalledExePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DualSenseBridge", "DualSenseBridge.exe");

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
        key.SetValue(ValueName, $"\"{target}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
