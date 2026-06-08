using System;
using System.IO;
using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SqlDeployer.ViewModels;
using SqlDeployerGui.Services;

namespace SqlDeployerGui.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel Vm => App.SettingsVm;

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = Vm;

        // About section assets + version (merged into the Settings screen).
        AppLogo.Source = LoadAsset("logo.png");
        SabarishPhoto.ProfilePicture = LoadAsset("sabarish.jpg");
        JaganPhoto.ProfilePicture = LoadAsset("jagan.jpg");

        var version = ResolveVersion();
        VersionLine.Text = $"Version {version}   •   Built {ResolveBuildDate():MMM d, yyyy}";
        AboutExpander.Description = $"Version {version} • developers";
    }

    private void AccentSwatch_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string preset)
            Vm.SelectedAccentPreset = preset;
    }

    private async void CheckUpdates_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateProgress.IsActive = true;
        UpdateProgress.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        UpdateStatusText.Text = "Checking…";

        var result = await App.Updates.CheckAndDownloadAsync();

        UpdateProgress.IsActive = false;
        UpdateProgress.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        CheckUpdatesButton.IsEnabled = true;

        switch (result.Status)
        {
            case UpdateStatus.UpToDate:
                UpdateStatusText.Text = "You're up to date.";
                break;
            case UpdateStatus.NotInstalled:
                UpdateStatusText.Text = "Updates apply only to the installed app.";
                break;
            case UpdateStatus.Failed:
                UpdateStatusText.Text = "Couldn't check — try again later.";
                break;
            case UpdateStatus.UpdateReady:
                UpdateStatusText.Text = $"Version {result.Version} downloaded.";
                await App.PromptRestartForUpdateAsync(result.Version!);
                break;
        }
    }

    private void CustomPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        var c = args.NewColor;
        Vm.CustomAccentColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        Vm.SelectedAccentPreset = "Custom";
    }

    private static BitmapImage LoadAsset(string fileName)
        => new(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", fileName)));

    private static string ResolveVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "2.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    // Build date = when the app assembly was last written, so it reflects the
    // running build without hardcoding a date.
    private static DateTime ResolveBuildDate()
    {
        try
        {
            var path = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return File.GetLastWriteTime(path);
        }
        catch
        {
            // Fall through to a safe default below.
        }
        return File.GetLastWriteTime(Path.Combine(AppContext.BaseDirectory, "SqlDeployerGui.dll"));
    }
}
