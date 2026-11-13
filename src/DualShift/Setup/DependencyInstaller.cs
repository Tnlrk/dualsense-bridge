using DualShift.Localization;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Nefarius.Drivers.HidHide;
using Nefarius.ViGEm.Client;

namespace DualShift.Setup;

public enum Dependency
{
    ViGEmBus,
    HidHide,
}

/// <summary>Detekce a instalace ovladačů přibalených v exe (Redist\*.exe jako embedded resources).</summary>
public static class DependencyInstaller
{
    public static bool IsInstalled(Dependency d) => d switch
    {
        Dependency.ViGEmBus => IsViGEmInstalled(),
        Dependency.HidHide => IsHidHideInstalled(),
        _ => false,
    };

    public static string DisplayName(Dependency d) => d switch
    {
        Dependency.ViGEmBus => Loc.T("dep_vigem"),
        Dependency.HidHide => Loc.T("dep_hidhide"),
        _ => d.ToString(),
    };

    private static bool IsViGEmInstalled()
    {
        try
        {
            using var client = new ViGEmClient();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsHidHideInstalled()
    {
        try
        {
            return new HidHideControlService().IsInstalled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Rozbalí přibalený instalátor do %TEMP% a spustí ho s právy správce (UAC).
    /// Vrací true, pokud instalátor doběhl s kódem 0 nebo 3010 (vyžaduje restart).
    /// </summary>
    public static async Task<InstallResult> InstallAsync(Dependency d)
    {
        string resource = d == Dependency.ViGEmBus ? "Redist.ViGEmBus_Setup.exe" : "Redist.HidHide_Setup.exe";
        string dir = Path.Combine(Path.GetTempPath(), "DualShift");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, resource["Redist.".Length..]);

        await using (var src = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
        {
            if (src is null) return InstallResult.Failed(Loc.T("dep_notBundled"));
            await using var dst = File.Create(file);
            await src.CopyToAsync(dst);
        }

        try
        {
            using var p = Process.Start(new ProcessStartInfo(file)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (p is null) return InstallResult.Failed(Loc.T("dep_notStarted"));
            await p.WaitForExitAsync();

            return p.ExitCode switch
            {
                0 => IsInstalled(d) ? InstallResult.Ok : InstallResult.NeedsRestart,
                3010 or 1641 => InstallResult.NeedsRestart,
                1602 or 1223 => InstallResult.Failed(Loc.T("dep_cancelled")),
                _ => IsInstalled(d) ? InstallResult.Ok : InstallResult.Failed(Loc.F("dep_exitCode", p.ExitCode)),
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return InstallResult.Failed(Loc.T("dep_uacDenied"));
        }
        catch (Exception ex)
        {
            return InstallResult.Failed(ex.Message);
        }
        finally
        {
            try { File.Delete(file); } catch { /* instalátor může ještě běžet */ }
        }
    }
}

public sealed record InstallResult(bool Success, bool RestartRequired, string? Error)
{
    public static readonly InstallResult Ok = new(true, false, null);
    public static readonly InstallResult NeedsRestart = new(true, true, null);
    public static InstallResult Failed(string error) => new(false, false, error);
}
