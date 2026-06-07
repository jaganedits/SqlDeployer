using CommunityToolkit.Mvvm.ComponentModel;
using SqlDeployer.Services;
using SqlDeployer.Theming;

namespace SqlDeployer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    // Raised when the theme changes so the app shell can re-apply RequestedTheme.
    public event EventHandler<string>? ThemeChanged;

    // Raised when any accent choice changes so the shell can re-apply the accent.
    public event EventHandler<AccentSelection>? AccentChanged;

    public string[] Themes { get; } = { "Default", "Light", "Dark" };

    // Preset ids the UI offers (named presets + "Custom").
    public string[] AccentPresets { get; } = { "Azure", "Emerald", "Indigo", "SlateAmber", "Custom" };

    [ObservableProperty] private string _selectedTheme = "Default";
    [ObservableProperty] private string _selectedAccentPreset = "Azure";
    [ObservableProperty] private string? _customAccentColor;
    [ObservableProperty] private bool _followSystemAccent;

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        var s = _settings.Load();
        _selectedTheme = s.Theme;
        _selectedAccentPreset = s.AccentPreset;
        _customAccentColor = s.CustomAccentColor;
        _followSystemAccent = s.FollowSystemAccent;
    }

    partial void OnSelectedThemeChanged(string value)
    {
        var s = _settings.Load();
        s.Theme = value;
        _settings.Save(s);
        ThemeChanged?.Invoke(this, value);
    }

    partial void OnSelectedAccentPresetChanged(string value) => PersistAccent();
    partial void OnCustomAccentColorChanged(string? value) => PersistAccent();
    partial void OnFollowSystemAccentChanged(bool value) => PersistAccent();

    private void PersistAccent()
    {
        var s = _settings.Load();
        s.AccentPreset = SelectedAccentPreset;
        s.CustomAccentColor = CustomAccentColor;
        s.FollowSystemAccent = FollowSystemAccent;
        _settings.Save(s);
        AccentChanged?.Invoke(this, new AccentSelection(SelectedAccentPreset, CustomAccentColor, FollowSystemAccent));
    }
}
