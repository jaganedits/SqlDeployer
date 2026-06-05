using System.IO;
using SqlDeployer.Services;
using SqlDeployer.ViewModels;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class SettingsViewModelTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "sqldeploy_sv_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Loads_saved_theme_on_construction()
    {
        var path = TempPath();
        var settings = new SettingsService(path);
        settings.Save(new SqlDeployer.Models.AppSettings { Theme = "Dark" });

        var vm = new SettingsViewModel(settings);

        Assert.Equal("Dark", vm.SelectedTheme);
        File.Delete(path);
    }

    [Fact]
    public void Changing_theme_persists_and_raises_event()
    {
        var path = TempPath();
        var settings = new SettingsService(path);
        var vm = new SettingsViewModel(settings);
        string? raised = null;
        vm.ThemeChanged += (_, theme) => raised = theme;

        vm.SelectedTheme = "Light";

        Assert.Equal("Light", new SettingsService(path).Load().Theme);
        Assert.Equal("Light", raised);
        File.Delete(path);
    }
}
