using DualSenseBridge.Localization;
using System.ComponentModel;
using System.Diagnostics;
using Nefarius.Drivers.HidHide;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace DualSenseBridge.HidHide;

/// <summary>
/// Skrývání originálního DualSense přes HidHide, aby hry viděly jen virtuální Xbox ovladač.
/// Změny konfigurace HidHide vyžadují práva správce, proto je provádí krátce spuštěná
/// kopie této aplikace s argumentem <see cref="ElevatedArg"/> (UAC).
/// </summary>
public static class HidHideService
{
    public const string ElevatedArg = "--hidhide";

    /// <summary>Instance ID (např. HID\{...}_VID&amp;0002054C...) z HID cesty zařízení.</summary>
    public static string? InstanceIdFromDevicePath(string devicePath)
    {
        try { return PnPDevice.GetInstanceIdFromInterfaceId(devicePath); }
        catch { return null; }
    }

    /// <summary>Cesty, které musí být na whitelistu, aby aplikace skrytý ovladač dál viděla.</summary>
    private static IEnumerable<string> OwnExecutablePaths()
    {
        if (Environment.ProcessPath is { } p) yield return p;
        yield return Autostart.InstalledExePath;
    }

    /// <summary>Spustí elevovanou kopii aplikace. Vrací true při úspěchu.</summary>
    public static bool RunElevated(bool hide, IEnumerable<string> instanceIds, out string? error)
    {
        error = null;
        var args = new List<string> { ElevatedArg, hide ? "hide" : "unhide" };
        args.AddRange(instanceIds);

        try
        {
            var psi = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(' ', args.Select(Quote)),
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                error = Loc.T("hh_notStarted");
                return false;
            }
            p.WaitForExit(30_000);
            if (p.ExitCode != 0) error = Loc.F("hh_failed", p.ExitCode);
            return p.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            error = Loc.T("hh_uacDenied");
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

    /// <summary>Tělo elevovaného procesu: <c>--hidhide hide|unhide id1 id2 …</c>.</summary>
    public static int RunElevatedCommand(string[] args)
    {
        if (args.Length < 2) return 2;
        try
        {
            var svc = new HidHideControlService();
            if (!svc.IsInstalled) return 3;

            bool hide = args[1] == "hide";
            var ids = args.Skip(2).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var blocked = new HashSet<string>(svc.BlockedInstanceIds, StringComparer.OrdinalIgnoreCase);

            if (hide)
            {
                var allowed = new HashSet<string>(svc.ApplicationPaths, StringComparer.OrdinalIgnoreCase);
                foreach (var path in OwnExecutablePaths())
                    if (!allowed.Contains(path))
                        svc.AddApplicationPath(path, throwIfInvalid: false);

                foreach (var id in ids)
                    if (!blocked.Contains(id))
                        svc.AddBlockedInstanceId(id);

                svc.IsActive = true;
            }
            else
            {
                foreach (var id in ids)
                    if (blocked.Contains(id))
                        svc.RemoveBlockedInstanceId(id);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return 1;
        }
    }
}
