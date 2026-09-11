namespace TravailCommePapa;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "TravailCommePapa_SingleInstance", out bool first);
        if (!first) return;

        ApplicationConfiguration.Initialize();

        // Quoi qu'il arrive, on rend au PC ses réglages d'origine
        AppDomain.CurrentDomain.ProcessExit += (_, _) => SystemGuard.Restore();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => SystemGuard.Restore();
        Application.ThreadException += (_, e) => System.Diagnostics.Debug.WriteLine(e.Exception);

        SystemGuard.Apply();
        try
        {
            Application.Run(new MainForm());
        }
        finally
        {
            SystemGuard.Restore();
        }
    }
}
