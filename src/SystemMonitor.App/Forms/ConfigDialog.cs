using System.Text.Json;
using SystemMonitor.Engine.Config;

namespace SystemMonitor.App.Forms;

public sealed class ConfigDialog : Form
{
    private readonly PropertyGrid _grid = new() { Dock = DockStyle.Fill };
    private readonly AppConfig _config;

    public ConfigDialog(AppConfig config)
    {
        _config = config;
        Text = "Configuration";
        ClientSize = new Size(640, 640);
        StartPosition = FormStartPosition.CenterParent;

        _grid.SelectedObject = _config;

        var save = new Button { Text = "Save", Dock = DockStyle.Bottom, Height = 32 };
        save.Click += (_, _) => SaveToDisk();

        var cancel = new Button { Text = "Cancel", Dock = DockStyle.Bottom, Height = 32 };
        cancel.Click += (_, _) => Close();

        Controls.Add(_grid);
        Controls.Add(save);
        Controls.Add(cancel);
    }

    private void SaveToDisk()
    {
        var path = "config.json";
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        MessageBox.Show(
            "Saved. Some changes apply immediately (thresholds); others (enabled collectors, intervals) require Stop + Start.",
            "Configuration saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
