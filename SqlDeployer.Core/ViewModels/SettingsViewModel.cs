using CommunityToolkit.Mvvm.ComponentModel;
using SqlDeployer.Services;

namespace SqlDeployer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    // Raised when the theme changes so the app shell can re-apply RequestedTheme.
    public event EventHandler<string>? ThemeChanged;

    public string[] Themes { get; } = { "Default", "Light", "Dark" };

    [ObservableProperty] private string _selectedTheme = "Default";

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        _selectedTheme = _settings.Load().Theme;
    }

    partial void OnSelectedThemeChanged(string value)
    {
        var s = _settings.Load();
        s.Theme = value;
        _settings.Save(s);
        ThemeChanged?.Invoke(this, value);
    }
}
