using System.Text.Json;
using SqlDeployer.Models;

namespace SqlDeployer.Services;

// Persists AppSettings as JSON. Because ConnectionProfile has no password
// property, secrets can never be written by construction.
public class SettingsService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public SettingsService(string filePath) => _filePath = filePath;

    // Default location: %LocalAppData%\SqlDeploy\settings.json
    public static SettingsService Default()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SqlDeploy");
        Directory.CreateDirectory(dir);
        return new SettingsService(Path.Combine(dir, "settings.json"));
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new AppSettings();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch
        {
            // Missing/corrupt settings should never crash the app.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, Options));
    }
}
