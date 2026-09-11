using System.Threading;

namespace CodexOledMonitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\CodexOledMonitorApp", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Codex OLED Monitor 已经在运行。", "Codex OLED Monitor",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.Contains("--minimized", StringComparer.OrdinalIgnoreCase)));
    }
}
