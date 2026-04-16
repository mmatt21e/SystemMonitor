using System.Windows.Forms;

namespace SystemMonitor.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        MessageBox.Show("SystemMonitor — scaffolding in place.", "SystemMonitor");
    }
}
