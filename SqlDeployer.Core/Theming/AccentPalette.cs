using System;
using System.Globalization;
using System.Linq;

namespace SqlDeployer.Theming;

// One accent color and its derived shade ramp. All colors are "#RRGGBB" (uppercase,
// no alpha). TextOnAccent is the foreground color to place on top of Base.
public sealed record AccentPalette(
    string Base,
    string Light1, string Light2, string Light3,
    string Dark1, string Dark2, string Dark3,
    string TextOnAccent);

// The user's accent choice, persisted in AppSettings and resolved by the GUI ThemeService.
public sealed record AccentSelection(string Preset, string? CustomColor, bool FollowSystem);

public static class AccentPalettes
{
    public const string DefaultPreset = "Azure";

    // Named preset base colors with an explicit (tinted) on-accent text color.
    private static readonly (string Id, string Base, string Text)[] Defs =
    {
        ("Azure",      "#2F6FED", "#FFFFFF"),
        ("Emerald",    "#10B981", "#04231A"),
        ("Indigo",     "#7C5CFC", "#FFFFFF"),
        ("SlateAmber", "#F59E0B", "#241A04"),
    };

    public static IReadOnlyList<string> PresetIds { get; } = Defs.Select(d => d.Id).ToList();

    public static AccentPalette ForPreset(string preset)
    {
        var def = Defs.FirstOrDefault(d =>
            string.Equals(d.Id, preset, StringComparison.OrdinalIgnoreCase));
        if (def.Id is null)
            def = Defs[0]; // unknown -> Azure
        return BuildRamp(def.Base) with { TextOnAccent = def.Text };
    }

    // Build a palette from any base color, computing the on-accent text by contrast.
    public static AccentPalette FromBaseColor(string hex) => BuildRamp(Normalize(hex));

    private static AccentPalette BuildRamp(string baseHex)
    {
        var (r, g, b) = ParseRgb(baseHex);
        return new AccentPalette(
            Base: Hex(r, g, b),
            Light1: Mix(r, g, b, 255, 0.20), Light2: Mix(r, g, b, 255, 0.40), Light3: Mix(r, g, b, 255, 0.60),
            Dark1: Mix(r, g, b, 0, 0.20),    Dark2: Mix(r, g, b, 0, 0.40),    Dark3: Mix(r, g, b, 0, 0.60),
            TextOnAccent: BestTextColor(r, g, b));
    }

    // Mix each channel toward `target` (0 or 255) by fraction t.
    private static string Mix(int r, int g, int b, int target, double t)
    {
        int M(int c) => (int)Math.Round(c * (1 - t) + target * t);
        return Hex(M(r), M(g), M(b));
    }

    // Choose white or near-black for maximum WCAG contrast against the base.
    private static string BestTextColor(int r, int g, int b)
    {
        var bg = RelLuminance(r, g, b);
        double Contrast(double a, double c) => (Math.Max(a, c) + 0.05) / (Math.Min(a, c) + 0.05);
        var white = Contrast(bg, 1.0);
        var black = Contrast(bg, RelLuminance(10, 10, 10));
        return white >= black ? "#FFFFFF" : "#0A0A0A";
    }

    private static double RelLuminance(int r, int g, int b)
    {
        double Lin(int c)
        {
            var s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);
    }

    public static (int R, int G, int B) ParseRgb(string hex)
    {
        var h = Normalize(hex);
        return (
            int.Parse(h.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(h.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(h.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    // Accepts "#RRGGBB" or "#AARRGGBB"; returns uppercase "#RRGGBB". Throws on bad input.
    private static string Normalize(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex[0] != '#')
            throw new FormatException($"Invalid color: '{hex}'.");
        var body = hex.Substring(1);
        if (body.Length == 8) body = body.Substring(2); // drop alpha
        if (body.Length != 6 || !body.All(Uri.IsHexDigit))
            throw new FormatException($"Invalid color: '{hex}'.");
        return "#" + body.ToUpperInvariant();
    }

    private static string Hex(int r, int g, int b) => $"#{r:X2}{g:X2}{b:X2}";
}
