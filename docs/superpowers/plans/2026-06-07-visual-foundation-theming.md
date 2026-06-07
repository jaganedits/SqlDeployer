# Visual Foundation — Theming & Design System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `SqlDeployerGui` a branded, modern theming foundation — an Azure brand accent with selectable presets + custom picker, Windows 11 Mica/Acrylic materials, compact spacing/radius/type tokens, and a theme-aware console.

**Architecture:** Pure, testable color logic lives in `SqlDeployer.Core/Theming` (no WinUI deps). A WinUI `ThemeService` in the GUI project resolves the selected accent, applies it by mutating long-lived accent brushes (so live preset switching works), and sets the Mica backdrop. A merged `Tokens.xaml` ResourceDictionary holds spacing/radius tokens and theme-aware console/log brushes. Settings persist three new `AppSettings` fields.

**Tech Stack:** .NET 10, WinUI 3 / Windows App SDK, CommunityToolkit.Mvvm, xUnit.

**Reference spec:** `docs/superpowers/specs/2026-06-07-visual-foundation-theming-design.md`

---

## File Structure

**Create**
- `SqlDeployer.Core/Theming/AccentPalette.cs` — `AccentPalette` record + `AccentSelection` record + `AccentPalettes` table/factory (pure color logic).
- `SqlDeployer.Core.Tests/AccentPaletteTests.cs` — unit tests for the above.
- `SqlDeployerGui/Services/ThemeService.cs` — applies accent + Mica to the running app.
- `SqlDeployerGui/Themes/Tokens.xaml` — spacing/radius tokens + theme-aware console/log brushes.

**Modify**
- `SqlDeployer.Core/Models/AppSettings.cs` — +`AccentPreset`, +`CustomAccentColor`, +`FollowSystemAccent`.
- `SqlDeployer.Core/ViewModels/SettingsViewModel.cs` — accent properties + `AccentChanged` event.
- `SqlDeployer.Core.Tests/SettingsViewModelTests.cs` — tests for the new VM behavior.
- `SqlDeployerGui/App.xaml` — merge `Tokens.xaml`; reference tokens from `Card`.
- `SqlDeployerGui/App.xaml.cs` — init `ThemeService` before window, apply backdrop, wire `AccentChanged`.
- `SqlDeployerGui/MainWindow.xaml` — transparent backgrounds so Mica shows.
- `SqlDeployerGui/Views/DeployPage.xaml` — token spacing + theme-aware console brushes.
- `SqlDeployerGui/Views/HistoryPage.xaml` — token spacing.
- `SqlDeployerGui/Views/SettingsPage.xaml` (+`.cs`) — accent swatches + custom picker + follow-system.
- `SqlDeployerGui/Converters/LogKindToBrushConverter.cs` — return theme-aware log brushes.

---

## Task 1: Core accent palette (pure color logic)

**Files:**
- Create: `SqlDeployer.Core/Theming/AccentPalette.cs`
- Test: `SqlDeployer.Core.Tests/AccentPaletteTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SqlDeployer.Core.Tests/AccentPaletteTests.cs`:

```csharp
using SqlDeployer.Theming;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class AccentPaletteTests
{
    [Theory]
    [InlineData("Azure", "#2F6FED", "#FFFFFF")]
    [InlineData("Emerald", "#10B981", "#04231A")]
    [InlineData("Indigo", "#7C5CFC", "#FFFFFF")]
    [InlineData("SlateAmber", "#F59E0B", "#241A04")]
    public void Preset_has_expected_base_and_text(string preset, string baseHex, string textOnAccent)
    {
        var p = AccentPalettes.ForPreset(preset);
        Assert.Equal(baseHex, p.Base);
        Assert.Equal(textOnAccent, p.TextOnAccent);
    }

    [Fact]
    public void Unknown_preset_falls_back_to_azure()
        => Assert.Equal("#2F6FED", AccentPalettes.ForPreset("nope").Base);

    [Fact]
    public void Ramp_lightens_and_darkens_around_base()
    {
        var p = AccentPalettes.ForPreset("Azure");
        Assert.True(ChannelSum(p.Light1) > ChannelSum(p.Base));
        Assert.True(ChannelSum(p.Light3) > ChannelSum(p.Light1));
        Assert.True(ChannelSum(p.Dark1) < ChannelSum(p.Base));
        Assert.True(ChannelSum(p.Dark3) < ChannelSum(p.Dark1));
    }

    [Fact]
    public void FromBaseColor_picks_dark_text_on_bright_color()
        => Assert.Equal("#0A0A0A", AccentPalettes.FromBaseColor("#FFD400").TextOnAccent);

    [Fact]
    public void FromBaseColor_picks_white_text_on_dark_color()
        => Assert.Equal("#FFFFFF", AccentPalettes.FromBaseColor("#101010").TextOnAccent);

    [Theory]
    [InlineData("not-a-color")]
    [InlineData("#12345")]
    [InlineData("")]
    public void FromBaseColor_rejects_bad_hex(string bad)
        => Assert.Throws<System.FormatException>(() => AccentPalettes.FromBaseColor(bad));

    private static int ChannelSum(string hex)
    {
        var (r, g, b) = AccentPalettes.ParseRgb(hex);
        return r + g + b;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --filter AccentPaletteTests --nologo`
Expected: FAIL to compile — `AccentPalettes` / `AccentPalette` do not exist.

- [ ] **Step 3: Write the implementation**

Create `SqlDeployer.Core/Theming/AccentPalette.cs`:

```csharp
using System.Globalization;

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
            string.Equals(d.Id, preset, System.StringComparison.OrdinalIgnoreCase));
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
        int M(int c) => (int)System.Math.Round(c * (1 - t) + target * t);
        return Hex(M(r), M(g), M(b));
    }

    // Choose white or near-black for maximum WCAG contrast against the base.
    private static string BestTextColor(int r, int g, int b)
    {
        var bg = RelLuminance(r, g, b);
        double Contrast(double a, double c) => (System.Math.Max(a, c) + 0.05) / (System.Math.Min(a, c) + 0.05);
        var white = Contrast(bg, 1.0);
        var black = Contrast(bg, RelLuminance(10, 10, 10));
        return white >= black ? "#FFFFFF" : "#0A0A0A";
    }

    private static double RelLuminance(int r, int g, int b)
    {
        double Lin(int c)
        {
            var s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : System.Math.Pow((s + 0.055) / 1.055, 2.4);
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
```

Note: add `using System;` and `using System.Linq;` at the top (for `FormatException`, `Uri.IsHexDigit`, `All`, `FirstOrDefault`, `Select`).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --filter AccentPaletteTests --nologo`
Expected: PASS (all AccentPaletteTests green).

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/Theming/AccentPalette.cs SqlDeployer.Core.Tests/AccentPaletteTests.cs
git commit -m "feat(theming): pure accent palette with presets and contrast-based text"
```

---

## Task 2: AppSettings accent fields

**Files:**
- Modify: `SqlDeployer.Core/Models/AppSettings.cs`
- Test: `SqlDeployer.Core.Tests/AppSettingsAccentTests.cs` (create)

- [ ] **Step 1: Write the failing tests**

Create `SqlDeployer.Core.Tests/AppSettingsAccentTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --filter AppSettingsAccentTests --nologo`
Expected: FAIL to compile — `AccentPreset` etc. don't exist on `AppSettings`.

- [ ] **Step 3: Add the fields**

In `SqlDeployer.Core/Models/AppSettings.cs`, add inside the `AppSettings` class (after `AutoOrderByDependencies`):

```csharp
    // Accent preset id: "Azure" | "Emerald" | "Indigo" | "SlateAmber" | "Custom".
    public string AccentPreset { get; set; } = "Azure";

    // "#RRGGBB" used only when AccentPreset == "Custom".
    public string? CustomAccentColor { get; set; }

    // When true, the accent tracks the OS accent color and AccentPreset is ignored.
    public bool FollowSystemAccent { get; set; } = false;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --filter AppSettingsAccentTests --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/Models/AppSettings.cs SqlDeployer.Core.Tests/AppSettingsAccentTests.cs
git commit -m "feat(settings): persist accent preset, custom color, follow-system flag"
```

---

## Task 3: SettingsViewModel accent properties + AccentChanged event

**Files:**
- Modify: `SqlDeployer.Core/ViewModels/SettingsViewModel.cs`
- Test: `SqlDeployer.Core.Tests/SettingsViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Append these tests inside the existing `SettingsViewModelTests` class in `SqlDeployer.Core.Tests/SettingsViewModelTests.cs` (uses the same `SettingsService(path)` pattern already in that file):

```csharp
    [Fact]
    public void Changing_accent_preset_persists_and_raises_event()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "svm_" + System.Guid.NewGuid().ToString("N") + ".json");
        var vm = new SqlDeployer.ViewModels.SettingsViewModel(new SettingsService(path));
        SqlDeployer.Theming.AccentSelection? raised = null;
        vm.AccentChanged += (_, sel) => raised = sel;

        vm.SelectedAccentPreset = "Indigo";

        Assert.Equal("Indigo", new SettingsService(path).Load().AccentPreset);
        Assert.NotNull(raised);
        Assert.Equal("Indigo", raised!.Preset);
        System.IO.File.Delete(path);
    }

    [Fact]
    public void Toggling_follow_system_persists_and_raises_event()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "svm_" + System.Guid.NewGuid().ToString("N") + ".json");
        var vm = new SqlDeployer.ViewModels.SettingsViewModel(new SettingsService(path));
        SqlDeployer.Theming.AccentSelection? raised = null;
        vm.AccentChanged += (_, sel) => raised = sel;

        vm.FollowSystemAccent = true;

        Assert.True(new SettingsService(path).Load().FollowSystemAccent);
        Assert.True(raised!.FollowSystem);
        System.IO.File.Delete(path);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --filter SettingsViewModelTests --nologo`
Expected: FAIL to compile — `AccentChanged` / `SelectedAccentPreset` / `FollowSystemAccent` don't exist.

- [ ] **Step 3: Extend the view-model**

Replace the body of `SqlDeployer.Core/ViewModels/SettingsViewModel.cs` with:

```csharp
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
```

Note: `using System;` is provided by the project's implicit usings (the original file relied on it for `EventHandler`).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --filter SettingsViewModelTests --nologo`
Expected: PASS.

- [ ] **Step 5: Run the full Core suite to confirm nothing regressed**

Run: `dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --nologo`
Expected: PASS (all tests).

- [ ] **Step 6: Commit**

```bash
git add SqlDeployer.Core/ViewModels/SettingsViewModel.cs SqlDeployer.Core.Tests/SettingsViewModelTests.cs
git commit -m "feat(settings): accent preset/custom/follow-system properties + AccentChanged"
```

---

## Task 4: Tokens.xaml — spacing/radius tokens + theme-aware console & log brushes

**Files:**
- Create: `SqlDeployerGui/Themes/Tokens.xaml`
- Modify: `SqlDeployerGui/App.xaml`

- [ ] **Step 1: Create the token dictionary**

Create `SqlDeployerGui/Themes/Tokens.xaml`:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <ResourceDictionary.ThemeDictionaries>
        <!-- Dark (also used for "Default") -->
        <ResourceDictionary x:Key="Default">
            <SolidColorBrush x:Key="ConsoleBackgroundBrush" Color="#FF0B0F14" />
            <SolidColorBrush x:Key="ConsoleBorderBrush" Color="#FF20262E" />
            <SolidColorBrush x:Key="ConsolePromptBackgroundBrush" Color="#FF0E141B" />
            <SolidColorBrush x:Key="ConsolePromptArrowBrush" Color="#FF4CC2FF" />
            <SolidColorBrush x:Key="ConsoleStatusBrush" Color="#FF8B95A3" />
            <SolidColorBrush x:Key="ConsoleCaretBrush" Color="#FFC8D0DA" />
            <SolidColorBrush x:Key="LogSuccessBrush" Color="#FF3FB950" />
            <SolidColorBrush x:Key="LogErrorBrush" Color="#FFF85149" />
            <SolidColorBrush x:Key="LogInfoBrush" Color="#FF9AA4B2" />
        </ResourceDictionary>
        <!-- Light -->
        <ResourceDictionary x:Key="Light">
            <SolidColorBrush x:Key="ConsoleBackgroundBrush" Color="#FFFFFFFF" />
            <SolidColorBrush x:Key="ConsoleBorderBrush" Color="#FFE2E4E9" />
            <SolidColorBrush x:Key="ConsolePromptBackgroundBrush" Color="#FFF5F6F8" />
            <SolidColorBrush x:Key="ConsolePromptArrowBrush" Color="#FF2F6FED" />
            <SolidColorBrush x:Key="ConsoleStatusBrush" Color="#FF5A6472" />
            <SolidColorBrush x:Key="ConsoleCaretBrush" Color="#FF1B2027" />
            <SolidColorBrush x:Key="LogSuccessBrush" Color="#FF1A7F37" />
            <SolidColorBrush x:Key="LogErrorBrush" Color="#FFD1242F" />
            <SolidColorBrush x:Key="LogInfoBrush" Color="#FF5A6472" />
        </ResourceDictionary>
        <!-- High contrast: reuse system brushes -->
        <ResourceDictionary x:Key="HighContrast">
            <SolidColorBrush x:Key="ConsoleBackgroundBrush" Color="{ThemeResource SystemColorWindowColor}" />
            <SolidColorBrush x:Key="ConsoleBorderBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
            <SolidColorBrush x:Key="ConsolePromptBackgroundBrush" Color="{ThemeResource SystemColorWindowColor}" />
            <SolidColorBrush x:Key="ConsolePromptArrowBrush" Color="{ThemeResource SystemColorHotlightColor}" />
            <SolidColorBrush x:Key="ConsoleStatusBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
            <SolidColorBrush x:Key="ConsoleCaretBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
            <SolidColorBrush x:Key="LogSuccessBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
            <SolidColorBrush x:Key="LogErrorBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
            <SolidColorBrush x:Key="LogInfoBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
        </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>

    <!-- Compact spacing / radius tokens (theme-independent) -->
    <Thickness x:Key="PagePadding">20,16,20,16</Thickness>
    <Thickness x:Key="CardPadding">12</Thickness>
    <x:Double x:Key="StackSpacingS">8</x:Double>
    <x:Double x:Key="StackSpacingM">12</x:Double>
    <x:Double x:Key="StackSpacingL">16</x:Double>
    <CornerRadius x:Key="CardCornerRadius">6</CornerRadius>

</ResourceDictionary>
```

- [ ] **Step 2: Merge it and use the radius/padding tokens in the Card style**

In `SqlDeployerGui/App.xaml`, change the `MergedDictionaries` block to add `Tokens.xaml`:

```xml
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
                <ResourceDictionary Source="ms-appx:///Themes/Tokens.xaml" />
            </ResourceDictionary.MergedDictionaries>
```

Then update the `Card` style in the same file to consume the tokens:

```xml
            <Style x:Key="Card" TargetType="Border">
                <Setter Property="Background" Value="{ThemeResource CardBackgroundFillColorDefaultBrush}" />
                <Setter Property="BorderBrush" Value="{ThemeResource CardStrokeColorDefaultBrush}" />
                <Setter Property="BorderThickness" Value="1" />
                <Setter Property="CornerRadius" Value="{StaticResource CardCornerRadius}" />
                <Setter Property="Padding" Value="{StaticResource CardPadding}" />
            </Style>
```

- [ ] **Step 3: Build to verify the dictionary loads**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -c Debug --nologo`
Expected: Build succeeds (no XAML parse errors).

- [ ] **Step 4: Launch and sanity-check**

Run: `SqlDeployerGui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\SqlDeployerGui.exe`
Expected: App opens normally; cards now have slightly tighter padding and 6px corners. Close the app.

- [ ] **Step 5: Commit**

```bash
git add SqlDeployerGui/Themes/Tokens.xaml SqlDeployerGui/App.xaml
git commit -m "feat(theming): add compact tokens and theme-aware console/log brushes"
```

---

## Task 5: ThemeService (apply accent + Mica)

**Files:**
- Create: `SqlDeployerGui/Services/ThemeService.cs`

- [ ] **Step 1: Write the service**

Create `SqlDeployerGui/Services/ThemeService.cs`:

```csharp
using Microsoft.UI;
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
        => res[key] = Parse(hex, 1.0);

    // Reuse the existing brush instance if present so live mutation reaches realized controls.
    private static void SetBrush(ResourceDictionary res, string key, string hex, double opacity)
    {
        var color = Parse(hex, 1.0);
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

    private static Color Parse(string hex, double _)
    {
        var (r, g, b) = AccentPalettes.ParseRgb(hex);
        return Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -c Debug --nologo`
Expected: Build succeeds. (Not yet wired into startup — Task 6.)

- [ ] **Step 3: Commit**

```bash
git add SqlDeployerGui/Services/ThemeService.cs
git commit -m "feat(theming): ThemeService applies accent brushes and Mica backdrop"
```

---

## Task 6: Wire ThemeService into startup

**Files:**
- Modify: `SqlDeployerGui/App.xaml.cs`

- [ ] **Step 1: Apply accent before the window, backdrop after, and wire AccentChanged**

In `SqlDeployerGui/App.xaml.cs`:

Add a static property next to the others (after `public static SettingsViewModel SettingsVm`):

```csharp
    public static ThemeService Theme { get; private set; } = null!;
```

Replace the body of `BuildAppBehindSplash` from the `Window = new MainWindow();` line down to (and including) the `SettingsVm.ThemeChanged += ...` line with:

```csharp
        Settings = SettingsService.Default();

        // Apply the saved accent BEFORE constructing the window so controls bind to
        // our accent brush instances (which we later mutate for live switching).
        Theme = new ThemeService(Settings);
        Theme.ApplyFromSettings();

        Window = new MainWindow();
        Theme.ApplyBackdrop(Window);

        var deployer = new SqlServerDeployer();
        var dialogs = new DialogService(Window);

        Deploy = new DeployViewModel(new DeploymentRunner(deployer), deployer, dialogs, Settings);
        History = new HistoryViewModel(deployer);
        SettingsVm = new SettingsViewModel(Settings);

        SettingsVm.ThemeChanged += (_, theme) => ApplyTheme(theme);
        SettingsVm.AccentChanged += (_, sel) => Theme.Apply(sel);
```

(The remaining splash-timer code below that block is unchanged.)

- [ ] **Step 2: Build**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -c Debug --nologo`
Expected: Build succeeds.

- [ ] **Step 3: Launch and verify accent + Mica**

Run: `SqlDeployerGui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\SqlDeployerGui.exe`
Expected:
- The **Deploy** button and **progress bar** are Azure blue (`#2F6FED`), not the OS accent.
- A subtle **Mica** tint is visible behind the nav rail / title bar (on Windows 11; if unsupported it's just solid — acceptable).
Close the app.

- [ ] **Step 4: Commit**

```bash
git add SqlDeployerGui/App.xaml.cs
git commit -m "feat(theming): apply accent at startup and Mica to the main window"
```

---

## Task 7: Transparent backgrounds so Mica shows through

**Files:**
- Modify: `SqlDeployerGui/MainWindow.xaml`

- [ ] **Step 1: Make the shell backgrounds transparent**

In `SqlDeployerGui/MainWindow.xaml`, set the root `Grid` and the `NavigationView` to transparent so the Mica backdrop is visible (cards/console keep their own solid brushes):

Change the root `<Grid>` opening tag (line 7) to:

```xml
    <Grid Background="Transparent">
```

Change the `NavigationView` opening tag to add a transparent background:

```xml
        <muxc:NavigationView x:Name="Nav" Grid.Row="1"
                             Background="Transparent"
                             IsBackButtonVisible="Collapsed"
                             IsSettingsVisible="False"
                             SelectionChanged="Nav_SelectionChanged"
                             PaneDisplayMode="Left">
```

- [ ] **Step 2: Build**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -c Debug --nologo`
Expected: Build succeeds.

- [ ] **Step 3: Launch and verify Mica depth**

Run: `SqlDeployerGui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\SqlDeployerGui.exe`
Expected: The window background and nav rail show the Mica desktop tint; content cards remain solid and readable. Move the window over a colorful desktop area to confirm the subtle tint shifts. Close the app.

- [ ] **Step 4: Commit**

```bash
git add SqlDeployerGui/MainWindow.xaml
git commit -m "feat(theming): transparent shell backgrounds for Mica"
```

---

## Task 8: DeployPage — token spacing + theme-aware console

**Files:**
- Modify: `SqlDeployerGui/Views/DeployPage.xaml`

- [ ] **Step 1: Use the page-padding token**

In `SqlDeployerGui/Views/DeployPage.xaml`, change the outer content grid (line 34) from `Padding="28,24,28,28"` to the token:

```xml
    <Grid Padding="{StaticResource PagePadding}" RowSpacing="20">
```

- [ ] **Step 1b: Tighten the form stacks (compact density)**

Apply the spacing tokens to the left-pane form. Change the form's outer `StackPanel` (line 61, `<StackPanel Spacing="16">`) to:

```xml
                <StackPanel Spacing="{StaticResource StackSpacingM}">
```

And change the CONNECTION card's inner `StackPanel` (line 64, `<StackPanel Spacing="14">`) to:

```xml
                        <StackPanel Spacing="{StaticResource StackSpacingS}">
```

- [ ] **Step 2: Make the console border theme-aware**

Replace the OUTPUT terminal border opening tag (the element with `RequestedTheme="Dark" Background="#FF0B0F14" BorderBrush="#FF20262E"`, ~line 210) with:

```xml
                    <Border Grid.Row="1" Margin="12,0,12,12" CornerRadius="6"
                            Background="{ThemeResource ConsoleBackgroundBrush}"
                            BorderBrush="{ThemeResource ConsoleBorderBrush}" BorderThickness="1">
```

(Removing `RequestedTheme="Dark"` lets the console follow the app theme.)

- [ ] **Step 3: Make the prompt line theme-aware**

Replace the prompt `Border` and its three `TextBlock`s (the block beginning `<Border Grid.Row="1" Background="#FF0E141B"`, ~line 280) with:

```xml
                            <Border Grid.Row="1" Background="{ThemeResource ConsolePromptBackgroundBrush}" Padding="16,8"
                                    BorderBrush="{ThemeResource ConsoleBorderBrush}" BorderThickness="0,1,0,0">
                                <StackPanel Orientation="Horizontal" Spacing="6">
                                    <TextBlock Text="&#x276F;" FontFamily="Cascadia Mono, Consolas" FontSize="13"
                                               Foreground="{ThemeResource ConsolePromptArrowBrush}" />
                                    <TextBlock Text="{Binding Status}" FontFamily="Cascadia Mono, Consolas" FontSize="13"
                                               Foreground="{ThemeResource ConsoleStatusBrush}" TextTrimming="CharacterEllipsis" />
                                    <TextBlock x:Name="Caret" Text="&#x2588;" FontFamily="Cascadia Mono, Consolas"
                                               FontSize="13" Foreground="{ThemeResource ConsoleCaretBrush}" />
                                </StackPanel>
                            </Border>
```

- [ ] **Step 4: Build**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -c Debug --nologo`
Expected: Build succeeds.

- [ ] **Step 5: Launch and verify the console in both themes**

Run: `SqlDeployerGui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\SqlDeployerGui.exe`
Expected: In Dark theme the console is the familiar near-black. Switch to **Light** via Settings → Theme: the OUTPUT console becomes a light surface with a blue prompt arrow and dark caret, still readable. Close the app.

- [ ] **Step 6: Commit**

```bash
git add SqlDeployerGui/Views/DeployPage.xaml
git commit -m "feat(theming): theme-aware console and token padding on Deploy page"
```

---

## Task 9: HistoryPage — token spacing

**Files:**
- Modify: `SqlDeployerGui/Views/HistoryPage.xaml`

- [ ] **Step 1: Use the page-padding token**

In `SqlDeployerGui/Views/HistoryPage.xaml`, change the outer grid (line 8) from `Padding="28,24,28,28"` to:

```xml
    <Grid Padding="{StaticResource PagePadding}" RowSpacing="16">
```

- [ ] **Step 2: Build and launch**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -c Debug --nologo`
Then: `SqlDeployerGui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\SqlDeployerGui.exe`
Expected: History page opens with the tighter compact padding consistent with Deploy. Close the app.

- [ ] **Step 3: Commit**

```bash
git add SqlDeployerGui/Views/HistoryPage.xaml
git commit -m "feat(theming): compact padding on History page"
```

---

## Task 10: SettingsPage — accent swatches + custom picker + follow-system

**Files:**
- Modify: `SqlDeployerGui/Views/SettingsPage.xaml`
- Modify: `SqlDeployerGui/Views/SettingsPage.xaml.cs`

- [ ] **Step 1: Add the accent UI to the Appearance card**

In `SqlDeployerGui/Views/SettingsPage.xaml`:

Change the page root padding (line 6) to the token:

```xml
    <ScrollViewer Padding="{StaticResource PagePadding}" VerticalScrollBarVisibility="Auto">
```

Replace the APPEARANCE card's inner `StackPanel` (the one containing the Theme `ComboBox`) with:

```xml
                <StackPanel Spacing="14">
                    <TextBlock Text="APPEARANCE" Style="{StaticResource SectionCaption}" />
                    <ComboBox Header="Theme" Width="240"
                              ItemsSource="{Binding Themes}"
                              SelectedItem="{Binding SelectedTheme, Mode=TwoWay}" />

                    <TextBlock Text="Accent" Style="{StaticResource Subtle}" FontSize="12" />
                    <StackPanel Orientation="Horizontal" Spacing="10" VerticalAlignment="Center">
                        <Button x:Name="SwAzure" Tag="Azure" Click="AccentSwatch_Click"
                                Width="28" Height="28" Padding="0" CornerRadius="14"
                                Background="#2F6FED" ToolTipService.ToolTip="Azure"
                                IsEnabled="{Binding FollowSystemAccent, Converter={StaticResource Not}}" />
                        <Button x:Name="SwEmerald" Tag="Emerald" Click="AccentSwatch_Click"
                                Width="28" Height="28" Padding="0" CornerRadius="14"
                                Background="#10B981" ToolTipService.ToolTip="Emerald"
                                IsEnabled="{Binding FollowSystemAccent, Converter={StaticResource Not}}" />
                        <Button x:Name="SwIndigo" Tag="Indigo" Click="AccentSwatch_Click"
                                Width="28" Height="28" Padding="0" CornerRadius="14"
                                Background="#7C5CFC" ToolTipService.ToolTip="Indigo"
                                IsEnabled="{Binding FollowSystemAccent, Converter={StaticResource Not}}" />
                        <Button x:Name="SwAmber" Tag="SlateAmber" Click="AccentSwatch_Click"
                                Width="28" Height="28" Padding="0" CornerRadius="14"
                                Background="#F59E0B" ToolTipService.ToolTip="Slate-Amber"
                                IsEnabled="{Binding FollowSystemAccent, Converter={StaticResource Not}}" />
                        <Button Width="28" Height="28" Padding="0" CornerRadius="14"
                                ToolTipService.ToolTip="Custom color"
                                IsEnabled="{Binding FollowSystemAccent, Converter={StaticResource Not}}">
                            <Button.Background>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                                    <GradientStop Color="#F85149" Offset="0" />
                                    <GradientStop Color="#2F6FED" Offset="0.5" />
                                    <GradientStop Color="#10B981" Offset="1" />
                                </LinearGradientBrush>
                            </Button.Background>
                            <Button.Flyout>
                                <Flyout>
                                    <ColorPicker x:Name="CustomPicker"
                                                 ColorSpectrumShape="Ring"
                                                 IsMoreButtonVisible="True"
                                                 IsColorChannelTextInputVisible="True"
                                                 IsAlphaEnabled="False"
                                                 ColorChanged="CustomPicker_ColorChanged" />
                                </Flyout>
                            </Button.Flyout>
                        </Button>
                        <TextBlock VerticalAlignment="Center" Style="{StaticResource Subtle}" FontSize="12"
                                   Text="{Binding SelectedAccentPreset}" />
                    </StackPanel>

                    <CheckBox Content="Follow system accent color"
                              IsChecked="{Binding FollowSystemAccent, Mode=TwoWay}" />
                </StackPanel>
```

Add the `Not` converter to the page resources. At the top of `SettingsPage.xaml`, add the converter namespace and a `Page.Resources` block (the page currently has no resources):

```xml
<Page
    x:Class="SqlDeployerGui.Views.SettingsPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:conv="using:SqlDeployerGui.Converters">

    <Page.Resources>
        <conv:BoolNegationConverter x:Key="Not" />
    </Page.Resources>
```

- [ ] **Step 2: Add the code-behind handlers**

In `SqlDeployerGui/Views/SettingsPage.xaml.cs`, add these handlers to the `SettingsPage` class (the page's DataContext is the `SettingsViewModel`; expose it as `Vm`). Add a `Vm` accessor if not present and the two handlers:

```csharp
    private SqlDeployer.ViewModels.SettingsViewModel Vm => App.SettingsVm;

    private void AccentSwatch_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Button b && b.Tag is string preset)
            Vm.SelectedAccentPreset = preset;
    }

    private void CustomPicker_ColorChanged(
        Microsoft.UI.Xaml.Controls.ColorPicker sender,
        Microsoft.UI.Xaml.Controls.ColorChangedEventArgs args)
    {
        var c = args.NewColor;
        Vm.CustomAccentColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        Vm.SelectedAccentPreset = "Custom";
    }
```

If `SettingsPage.xaml.cs` does not already set `DataContext`, add `DataContext = Vm;` in its constructor after `InitializeComponent();` (mirror how `DeployPage` wires its VM).

- [ ] **Step 3: Build**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -c Debug --nologo`
Expected: Build succeeds.

- [ ] **Step 4: Launch and verify live accent switching**

Run: `SqlDeployerGui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\SqlDeployerGui.exe`
Expected, on Settings:
- Clicking **Emerald** turns the Deploy button / progress / nav selection green **immediately** (no restart). The label next to the swatches shows the current preset.
- Opening the **Custom** swatch and picking a color applies it live and sets the label to "Custom".
- Ticking **Follow system accent** disables the swatches and applies the OS accent.
- Restart the app — the last choice persists.

> **Live-update fallback:** if the nav selection indicator does *not* recolor live (only after navigating away and back), add this to `ThemeService.Apply` after the brush mutations — re-assert the root theme to force a pass:
> ```csharp
> if (App.Window?.Content is FrameworkElement fe)
> {
>     var t = fe.RequestedTheme;
>     fe.RequestedTheme = ElementTheme.Default;
>     fe.RequestedTheme = t;
> }
> ```
> Only add this if the manual check shows a control not updating; it is a no-op cost otherwise but can cause a brief flicker, so prefer omitting it.

- [ ] **Step 5: Commit**

```bash
git add SqlDeployerGui/Views/SettingsPage.xaml SqlDeployerGui/Views/SettingsPage.xaml.cs
git commit -m "feat(theming): accent swatches, custom picker, follow-system in Settings"
```

---

## Task 11: Theme-aware log brushes in the converter

**Files:**
- Modify: `SqlDeployerGui/Converters/LogKindToBrushConverter.cs`

- [ ] **Step 1: Resolve log colors from theme-aware resources**

Replace the `Convert` method body of `SqlDeployerGui/Converters/LogKindToBrushConverter.cs` so it returns the theme brushes defined in `Tokens.xaml` (keys `LogSuccessBrush` / `LogErrorBrush` / `LogInfoBrush`) instead of hardcoded colors. The converter receives a `LogKind` value; map it to a resource key and look it up:

```csharp
public object Convert(object value, Type targetType, object parameter, string language)
{
    var key = value switch
    {
        LogKind.Success => "LogSuccessBrush",
        LogKind.Error => "LogErrorBrush",
        _ => "LogInfoBrush",
    };
    return Application.Current.Resources.TryGetValue(key, out var brush)
        ? brush
        : Application.Current.Resources["LogInfoBrush"];
}
```

Ensure the file's `using`s include `using Microsoft.UI.Xaml;` (for `Application`) and `using SqlDeployer.Models;` (for `LogKind`). Keep the existing `ConvertBack` (throw `NotImplementedException`) and namespace.

- [ ] **Step 2: Build**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj -c Debug --nologo`
Expected: Build succeeds.

- [ ] **Step 3: Launch and verify log colors in both themes**

Run: `SqlDeployerGui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\SqlDeployerGui.exe`
Expected: Run a deploy (or trigger a folder scan) to produce log lines. Success lines are green, errors red, info muted — and switching Theme to Light keeps them readable (darker green/red on the light console). Close the app.

- [ ] **Step 4: Commit**

```bash
git add SqlDeployerGui/Converters/LogKindToBrushConverter.cs
git commit -m "feat(theming): theme-aware log entry colors"
```

---

## Final verification

- [ ] **Step 1: Run the full Core test suite**

Run: `dotnet test SqlDeployer.Core.Tests/SqlDeployer.Core.Tests.csproj --nologo`
Expected: PASS (all tests, including the new AccentPalette / AppSettings / SettingsViewModel tests).

- [ ] **Step 2: Full manual pass**

Run: `SqlDeployerGui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\SqlDeployerGui.exe`
Walk the checklist:
- Azure accent on Deploy button / progress / nav selection / focus rings.
- Mica tint behind nav + title bar (or graceful solid fallback).
- Switch presets in Settings → live recolor; Custom picker works; Follow-system tracks OS accent; choice persists across restart.
- Theme Light/Dark → console + log colors adapt and stay readable.
- Compact spacing consistent across Deploy / History / Settings.

- [ ] **Step 3: Confirm clean tree**

Run: `git status --short`
Expected: clean (all changes committed).
```
