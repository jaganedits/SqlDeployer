using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SqlDeployer.Services;
using SqlDeployer.Theming;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace SqlDeployerGui.Services;

// Applies the selected accent to the running app and the Mica backdrop to a window.
// Accent brushes are created once and then mutated (.Color) so already-realized
// controls update live when the user switches presets.
public sealed class ThemeService
{
    private readonly SettingsService _settings;
    private readonly UISettings _ui = new();

    public ThemeService(SettingsService settings) => _settings = settings;

    public void ApplyFromSettings()
    {
        var s = _settings.Load();
        Apply(new AccentSelection(s.AccentPreset, s.CustomAccentColor, s.FollowSystemAccent));
    }

    public void Apply(AccentSelection selection)
    {
        var palette = Resolve(selection);
        var res = Application.Current.Resources;

        SetColor(res, "SystemAccentColor", palette.Base);
        SetColor(res, "SystemAccentColorLight1", palette.Light1);
        SetColor(res, "SystemAccentColorLight2", palette.Light2);
        SetColor(res, "SystemAccentColorLight3", palette.Light3);
        SetColor(res, "SystemAccentColorDark1", palette.Dark1);
        SetColor(res, "SystemAccentColorDark2", palette.Dark2);
        SetColor(res, "SystemAccentColorDark3", palette.Dark3);

        SetBrush(res, "AccentFillColorDefaultBrush", palette.Base, 1.0);
        SetBrush(res, "AccentFillColorSecondaryBrush", palette.Base, 0.9);
        SetBrush(res, "AccentFillColorTertiaryBrush", palette.Base, 0.8);
        SetBrush(res, "TextOnAccentFillColorPrimaryBrush", palette.TextOnAccent, 1.0);
    }

    public void ApplyBackdrop(Window window)
    {
        if (MicaController.IsSupported())
            window.SystemBackdrop = new MicaBackdrop();
        // else: WinUI renders the solid theme brush automatically.
    }

    private AccentPalette Resolve(AccentSelection s)
    {
        if (s.FollowSystem)
        {
            var c = _ui.GetColorValue(UIColorType.Accent);
            return AccentPalettes.FromBaseColor($"#{c.R:X2}{c.G:X2}{c.B:X2}");
        }
        if (string.Equals(s.Preset, "Custom", System.StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(s.CustomColor))
        {
            try { return AccentPalettes.FromBaseColor(s.CustomColor!); }
            catch { return AccentPalettes.ForPreset(AccentPalettes.DefaultPreset); }
        }
        return AccentPalettes.ForPreset(s.Preset);
    }

    private static void SetColor(ResourceDictionary res, string key, string hex)
        => res[key] = Parse(hex);

    // Reuse the existing brush instance if present so live mutation reaches realized controls.
    private static void SetBrush(ResourceDictionary res, string key, string hex, double opacity)
    {
        var color = Parse(hex);
        if (res.TryGetValue(key, out var existing) && existing is SolidColorBrush b)
        {
            b.Color = color;
            b.Opacity = opacity;
        }
        else
        {
            res[key] = new SolidColorBrush(color) { Opacity = opacity };
        }
    }

    private static Color Parse(string hex)
    {
        var (r, g, b) = AccentPalettes.ParseRgb(hex);
        return Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
    }
}
