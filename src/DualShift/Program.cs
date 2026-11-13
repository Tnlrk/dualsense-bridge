using DualShift.Localization;
using System.Windows.Forms;
using DualShift.HidHide;

namespace DualShift;

internal static class Program
{
    /// <summary>Argument, se kterým aplikaci spouští Windows po přihlášení – zůstane jen v liště.</summary>
    public const string MinimizedArg = "--minimized";

    /// <summary>Signál od dalšího spuštění: „ukaž okno“.</summary>
    public const string ShowEventName = @"Local\DualShift.Show";

    [STAThread]
    private static int Main(string[] args)
    {
        // Krátce běžící elevovaná kopie pro změnu konfigurace HidHide.
        if (args.Length > 0 && args[0] == HidHideService.ElevatedArg)
            return HidHideService.RunElevatedCommand(args);

        Loc.SetLanguage(AppSettings.Load().Language);
        bool minimized = args.Contains(MinimizedArg, StringComparer.OrdinalIgnoreCase);

        using var mutex = new Mutex(true, @"Local\DualShift.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            // Aplikace už běží – jen ji požádat, ať ukáže okno.
            if (!minimized)
            {
                try
                {
                    using var show = EventWaitHandle.OpenExisting(ShowEventName);
                    show.Set();
                }
                catch
                {
                    MessageBox.Show(Loc.T("dlg_alreadyRunning"), "DualShift", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            return 0;
        }

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext(showEvent, startMinimized: minimized));
        return 0;
    }
}
