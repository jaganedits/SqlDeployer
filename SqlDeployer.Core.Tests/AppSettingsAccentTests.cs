using System.IO;
using SqlDeployer.Models;
using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class AppSettingsAccentTests
{
    [Fact]
    public void Accent_fields_round_trip()
    {
        var path = Path.Combine(Path.GetTempPath(), "acc_" + System.Guid.NewGuid().ToString("N") + ".json");
        var svc = new SettingsService(path);
        var s = svc.Load();
        s.AccentPreset = "Indigo";
        s.CustomAccentColor = "#123456";
        s.FollowSystemAccent = true;
        svc.Save(s);

        var reloaded = new SettingsService(path).Load();
        Assert.Equal("Indigo", reloaded.AccentPreset);
        Assert.Equal("#123456", reloaded.CustomAccentColor);
        Assert.True(reloaded.FollowSystemAccent);
        File.Delete(path);
    }

    [Fact]
    public void Legacy_settings_without_accent_fields_use_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "legacy_" + System.Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{ \"Theme\": \"Dark\" }"); // no accent keys
        var s = new SettingsService(path).Load();
        Assert.Equal("Azure", s.AccentPreset);
        Assert.Null(s.CustomAccentColor);
        Assert.False(s.FollowSystemAccent);
        File.Delete(path);
    }
}
