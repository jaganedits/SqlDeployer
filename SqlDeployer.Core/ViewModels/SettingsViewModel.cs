using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SqlDeployer.Models;
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

    // Raised when the user picks a saved connection to load onto the Deploy form.
    public event EventHandler<ConnectionProfile>? LoadConnectionRequested;

    // Raised (with the server name) when a saved connection is deleted, so the
    // Deploy view model can drop it from its autocomplete list.
    public event EventHandler<string>? ConnectionDeleted;

    // The saved connections shown in Settings. Mirrors AppSettings.SavedConnections.
    public ObservableCollection<ConnectionProfile> SavedConnections { get; } = new();

    public bool HasSavedConnections => SavedConnections.Count > 0;

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

        RefreshSavedConnections();
    }

    // Reloads the saved-connection list from disk. Called on construction and when
    // the Settings page is shown, so connections saved on the Deploy page appear.
    public void RefreshSavedConnections()
    {
        SavedConnections.Clear();
        foreach (var c in _settings.Load().SavedConnections)
            SavedConnections.Add(c);
        OnPropertyChanged(nameof(HasSavedConnections));
    }

    // Asks the shell to load this connection onto the Deploy form.
    public void LoadConnection(ConnectionProfile profile)
        => LoadConnectionRequested?.Invoke(this, profile);

    // Removes a saved connection from disk and the list.
    public void DeleteConnection(ConnectionProfile profile)
    {
        var s = _settings.Load();
        s.SavedConnections.RemoveAll(c =>
            string.Equals(c.Server, profile.Server, StringComparison.OrdinalIgnoreCase));

        // Don't let a deleted server come back as the startup prefill.
        if (s.LastConnection is not null &&
            string.Equals(s.LastConnection.Server, profile.Server, StringComparison.OrdinalIgnoreCase))
            s.LastConnection = null;

        _settings.Save(s);

        var existing = SavedConnections.FirstOrDefault(c =>
            string.Equals(c.Server, profile.Server, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) SavedConnections.Remove(existing);
        OnPropertyChanged(nameof(HasSavedConnections));

        ConnectionDeleted?.Invoke(this, profile.Server);
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
