using System.IO;
using SqlDeployer.Models;
using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class SettingsServiceTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "sqldeploy_test_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        var path = TempFile();
        var svc = new SettingsService(path);

        var settings = svc.Load();

        Assert.NotNull(settings);
        Assert.Null(settings.LastConnection);
        Assert.Equal("Default", settings.Theme);
    }

    [Fact]
    public void Save_then_Load_round_trips_values()
    {
        var path = TempFile();
        var svc = new SettingsService(path);
        var settings = new AppSettings
        {
            Theme = "Dark",
            LastConnection = new ConnectionProfile
            {
                Server = "localhost",
                Login = "sa",
                Database = "AppDb",
                ScriptPath = @"C:\scripts"
            }
        };

        svc.Save(settings);
        var loaded = new SettingsService(path).Load();

        Assert.Equal("Dark", loaded.Theme);
        Assert.Equal("localhost", loaded.LastConnection!.Server);
        Assert.Equal("AppDb", loaded.LastConnection.Database);

        File.Delete(path);
    }

    [Fact]
    public void Saved_json_never_contains_a_password()
    {
        var path = TempFile();
        var svc = new SettingsService(path);
        svc.Save(new AppSettings
        {
            LastConnection = new ConnectionProfile { Server = "s", Login = "sa", Database = "d", ScriptPath = "p" }
        });

        var json = File.ReadAllText(path);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);

        File.Delete(path);
    }

    [Fact]
    public void Load_returns_defaults_when_file_is_corrupt()
    {
        var path = TempFile();
        File.WriteAllText(path, "{ not valid json ");

        var settings = new SettingsService(path).Load();

        Assert.Equal("Default", settings.Theme);
        File.Delete(path);
    }
}
