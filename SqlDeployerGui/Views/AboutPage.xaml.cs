using System;
using System.IO;
using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SqlDeployerGui.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();

        AppLogo.Source = LoadAsset("logo.png");
        SabarishPhoto.ProfilePicture = LoadAsset("sabarish.jpg");
        JaganPhoto.ProfilePicture = LoadAsset("jagan.jpg");

        VersionLine.Text = $"Version {ResolveVersion()}   •   Built {ResolveBuildDate():MMM d, yyyy}";
    }

    private static BitmapImage LoadAsset(string fileName)
        => new(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", fileName)));

    private static string ResolveVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "2.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    // Build date = when the app assembly was last written. Reflects the build that
    // produced the running exe, so it stays accurate without hardcoding a date.
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
