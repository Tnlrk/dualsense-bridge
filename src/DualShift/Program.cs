using DualShift.Localization;
using System.Windows.Forms;
using DualShift.HidHide;

namespace DualShift;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Krátce běžící elevovaná kopie pro změnu konfigurace HidHide.
        if (args.Length > 0 && args[0] == HidHideService.ElevatedArg)
            return HidHideService.RunElevatedCommand(args);

        Loc.SetLanguage(AppSettings.Load().Language);

        using var mutex = new Mutex(true, @"Local\DualShift.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show(Loc.T("dlg_alreadyRunning"),
                "DualShift", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
        return 0;
    }
}
