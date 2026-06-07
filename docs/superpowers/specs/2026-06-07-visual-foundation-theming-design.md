# Visual Foundation — Theming & Design System (Design Spec)

**Date:** 2026-06-07
**Status:** Approved (look & feel validated via visual companion)
**Scope:** Sub-project 1 of 3 in the "professional modern UI" effort.

## Context

`SqlDeployerGui` is a WinUI 3 (.NET 10, Windows App SDK) Fluent app with three
pages (Deploy / History / Settings) hosted in a `NavigationView` with a custom
title bar. It already supports Light/Dark/Default theming via
`SettingsViewModel.ThemeChanged` → `App.ApplyTheme` (sets `RequestedTheme` on the
root). Styling today is ad-hoc: a few brushes/styles in `App.xaml` (`Card`,
`SectionCaption`, `Subtle`), hardcoded console colors in `DeployPage.xaml`, and
the generic OS system accent for buttons/nav/progress.

This spec defines a **theming foundation** that everything else (Component Polish,
Connection Manager) will build on: a brand accent with presets + custom picker,
Windows 11 Mica/Acrylic materials, a compact spacing/typography/radius token set,
and a theme-aware console.

## Goals

- A defined **brand accent (Azure "Deploy Blue", base `#2F6FED`)** applied to the
  Deploy button, nav selection, progress, focus, links, and other accent surfaces.
- **Selectable accent presets** in Settings: Azure (default), Emerald, Indigo,
  Slate-Amber, a **custom accent color picker**, and **Follow system accent**.
- Live preset switching (no restart).
- **Mica** backdrop on the main window + **Acrylic** for transient surfaces
  (flyouts, dropdowns), with automatic fallback where unsupported.
- **Compact** spacing density, applied consistently via tokens.
- **Theme-aware console**: the OUTPUT pane is dark in dark mode and a light
  surface in light mode (today it is force-dark).
- Success = green, error = red remain fixed and theme-aware (status is always
  unambiguous, independent of accent).

## Non-Goals (handled by later sub-projects)

- History grid / status pills / search / export, live per-script checklist, empty
  states, completion toast, keyboard shortcuts → **Component Polish** (spec 2).
- Connection / environment manager → **spec 3**.
- New functional features (dry-run preview, rollback runner, view/diff SQL) were
  explicitly deselected and are out of scope.

## Decisions (from brainstorming)

| Decision | Choice |
|---|---|
| Brand accent | Azure "Deploy Blue", base `#2F6FED` |
| Backdrop | Mica on window + Acrylic flyouts |
| Light-mode console | Theme-aware (light console in light mode) |
| Presets | Azure default + Emerald + Indigo + Slate-Amber + custom picker + Follow system |
| Density | Compact |

## Architecture

Three layers, separated so the color logic is testable without WinUI:

1. **`SqlDeployer.Core/Theming/` (pure, testable, no WinUI deps)**
   - `AccentPalette` — a record describing one accent: `Base`, `Light1/2/3`,
     `Dark1/2/3` (shade ramp), and `TextOnAccent` (white or near-black, chosen for
     contrast). Colors stored as `#AARRGGBB` strings or a small `(byte R,G,B)` struct.
   - `AccentPalettes` — the named preset table (Azure/Emerald/Indigo/Slate-Amber)
     and a `FromBaseColor(hex)` factory that computes the ramp + picks
     `TextOnAccent` by luminance, used for the custom picker.
   - Pure shade math (lighten/darken toward white/black by fixed steps) so presets
     and custom colors produce a consistent ramp.

2. **`SqlDeployerGui/Services/ThemeService.cs` (WinUI glue)**
   - Resolves the **effective** accent: `FollowSystemAccent` → OS `UISettings`
     accent; else `Custom` → `CustomAccentColor`; else the named preset's base.
   - Converts the `AccentPalette` to `Windows.UI.Color` and applies it by
     overriding the framework accent resources (see "Accent application").
   - Applies the **Mica** backdrop on a window (feature-detected).
   - Persists accent settings via `SettingsService`.
   - Invoked at startup and whenever the Settings accent UI changes.

3. **`SqlDeployerGui/Themes/Tokens.xaml` (ResourceDictionary, merged in `App.xaml`)**
   - Spacing / corner-radius / type tokens (compact).
   - Theme-aware **console brushes** as `ThemeDictionaries` (Light/Dark).
   - Theme-aware **log brushes** (success/error/info) referenced by the converter.

### Accent application

WinUI derives `AccentFillColorDefaultBrush` / `Secondary` / `Tertiary` and
`TextOnAccentFillColorPrimaryBrush` (used by `AccentButtonStyle`, `ProgressBar`,
`NavigationView` selection, `ToggleSwitch`, etc.) from the `SystemAccentColor*`
color resources. `ThemeService` will, at app-resource scope:

- Set `SystemAccentColor`, `SystemAccentColorLight1/2/3`, `SystemAccentColorDark1/2/3`.
- Set the derived accent **brush** resources (`AccentFillColorDefaultBrush` and
  family) and `TextOnAccentFillColorPrimaryBrush` (so Emerald/Amber get dark text
  for contrast, Azure/Indigo get white).

**Live-update risk (validate early):** replacing dictionary entries at runtime does
not always re-flow already-realized controls. Mitigation: hold the accent brushes
as long-lived `SolidColorBrush` instances in `Application.Current.Resources` and,
on change, mutate their `.Color` (bound controls observe the change) rather than
swapping the resource. If a control still doesn't refresh, the fallback is to
re-assert `RequestedTheme` on the root to force a theme pass. The implementation
plan must spike this on a `ProgressBar` + `AccentButton` + nav selection first.

### Azure ramp (default brand)

| Token | Hex |
|---|---|
| Base (`SystemAccentColor`) | `#2F6FED` |
| Light1 / 2 / 3 (dark-theme shades) | `#4C82F0` / `#6E9AF3` / `#93B4F7` |
| Dark1 / 2 / 3 (light-theme shades) | `#2A63D6` / `#2455BC` / `#1E47A0` |
| TextOnAccent | `#FFFFFF` |

### Preset base colors

| Preset | Base | TextOnAccent |
|---|---|---|
| Azure (default) | `#2F6FED` | white |
| Emerald | `#10B981` | near-black `#04231A` |
| Indigo | `#7C5CFC` | white |
| Slate-Amber | `#F59E0B` | near-black `#241A04` |

Custom picker: ramp + TextOnAccent computed by `AccentPalettes.FromBaseColor`.

### Compact tokens

Spacing (replaces today's 16/20/28 rhythm):

| Token | Value |
|---|---|
| `SpacingXs` | 4 |
| `SpacingS` | 8 |
| `SpacingM` | 12 |
| `SpacingL` | 16 |
| `SpacingXl` | 20 |

- Page padding `28,24,28,28` → `20,16,20,16`.
- Card padding `20` → `12`; card `CornerRadius` `8` → `6` (`CardCornerRadius` token).
- Inter-control `Spacing` in form stacks `14–16` → `8–10`.
- Reuse the Fluent type ramp; keep the existing semantic styles (`SectionCaption`,
  `Subtle`) and add a `PageTitle` style sized for compact.

### Theme-aware console

Move the hardcoded `DeployPage.xaml` console colors to `ThemeDictionaries` keys
(`ConsoleBackgroundBrush`, `ConsoleBorderBrush`, `ConsolePromptBackgroundBrush`,
`ConsolePromptArrowBrush`, `ConsoleStatusBrush`, `ConsoleCaretBrush`) and remove
`RequestedTheme="Dark"` from the OUTPUT border.

| Key | Dark | Light |
|---|---|---|
| ConsoleBackground | `#0B0F14` | `#FFFFFF` |
| ConsoleBorder | `#20262E` | `#E2E4E9` |
| ConsolePromptBackground | `#0E141B` | `#F5F6F8` |
| ConsolePromptArrow | `#4CC2FF` | `#2F6FED` |
| ConsoleStatus | `#8B95A3` | `#5A6472` |
| ConsoleCaret | `#C8D0DA` | `#1B2027` |

`LogKindToBrushConverter` returns theme-aware log brushes (`LogSuccessBrush` /
`LogErrorBrush` / `LogInfoBrush`) defined per-theme, instead of hardcoded colors.

### Mica + Acrylic

- `MainWindow`: `SystemBackdrop = new MicaBackdrop()` (Base). Make the root `Grid`
  background transparent and the `NavigationView` pane background transparent so
  Mica shows through; **cards stay solid** (`CardBackgroundFillColorDefaultBrush`)
  for readability.
- Acrylic: WinUI flyouts / `ComboBox` dropdowns / menus already use Acrylic on
  Win11; the work is ensuring no opaque page background blocks it. Optionally set
  the nav pane to Acrylic for extra depth.
- **Fallback:** feature-detect `MicaController.IsSupported()`; if false (older
  Windows / some RDP), skip the backdrop — WinUI renders the solid theme brush.
- `SplashWindow`: Mica optional (nice-to-have, not required).

### Settings UI (Appearance)

Extend the existing APPEARANCE card in `SettingsPage.xaml`:

- Keep the **Theme** combo (Default/Light/Dark).
- Add **Accent**: a horizontal row of preset swatches (Azure/Emerald/Indigo/
  Slate-Amber) with the selected one ringed, a **Custom** swatch opening a
  `ColorPicker` flyout, and a **"Follow system accent color"** toggle (which
  disables the preset/custom selection while on).
- `SettingsViewModel` gains: `AccentPresets`, `SelectedAccentPreset`,
  `CustomAccentColor`, `FollowSystemAccent`, and an `AccentChanged` event
  (mirroring the existing `ThemeChanged` pattern) that `App` wires to
  `ThemeService.ApplyAccent(...)`.

## Data model & persistence

`SqlDeployer.Core/Models/AppSettings.cs` gains:

```csharp
public string AccentPreset { get; set; } = "Azure";      // "Azure" | "Emerald" | "Indigo" | "SlateAmber" | "Custom"
public string? CustomAccentColor { get; set; }           // "#AARRGGBB" when AccentPreset == "Custom"
public bool FollowSystemAccent { get; set; } = false;
```

**Back-compat:** legacy settings JSON without these fields deserialize to the
defaults (Azure, not following system). This intentionally rebrands existing
installs from the generic system accent to Azure on first launch after upgrade.

## App startup integration

In `App.BuildAppBehindSplash`: construct `ThemeService(Settings)`, apply the saved
accent before/at `Window` activation (alongside the existing `ApplyTheme`), set the
Mica backdrop on `MainWindow`, and wire `SettingsVm.AccentChanged → ThemeService`.

## Error handling

- Invalid/unparseable `CustomAccentColor` → fall back to Azure and log (don't crash).
- Mica unsupported → silent fallback to solid theme brush.
- `UISettings` accent unavailable (rare) → fall back to Azure.
- Accent apply is best-effort and wrapped so a theming failure never blocks deploys.

## Testing strategy

**Unit (Core, no WinUI):**
- `AccentPaletteTests`: each named preset resolves to its expected base + ramp;
  `TextOnAccent` picks white vs near-black by luminance (Azure/Indigo → white,
  Emerald/Amber → dark); `FromBaseColor` produces a monotonic ramp; invalid hex
  → Azure fallback.
- `AppSettings` round-trip: new fields persist; legacy JSON (missing fields) loads
  with correct defaults. Extend existing `SettingsService`/`SettingsViewModel` tests.

**Manual (run the app):** Mica visible behind nav/title bar; Azure accent on Deploy
button / nav selection / progress / focus; preset switch updates live; custom
picker applies; Follow-system tracks the OS accent; light vs dark console; compact
spacing across all three pages; Mica fallback (e.g. over RDP) renders solid.

## Files

**Create**
- `SqlDeployer.Core/Theming/AccentPalette.cs`
- `SqlDeployer.Core.Tests/AccentPaletteTests.cs`
- `SqlDeployerGui/Services/ThemeService.cs`
- `SqlDeployerGui/Themes/Tokens.xaml`

**Modify**
- `SqlDeployer.Core/Models/AppSettings.cs` (+3 fields)
- `SqlDeployer.Core/ViewModels/SettingsViewModel.cs` (accent props + `AccentChanged`)
- `SqlDeployerGui/App.xaml` (merge `Tokens.xaml`)
- `SqlDeployerGui/App.xaml.cs` (init `ThemeService`, Mica, wire `AccentChanged`)
- `SqlDeployerGui/MainWindow.xaml(.cs)` (Mica backdrop, transparent backgrounds)
- `SqlDeployerGui/Views/DeployPage.xaml` (token spacing; theme-aware console brushes)
- `SqlDeployerGui/Views/HistoryPage.xaml` (token spacing)
- `SqlDeployerGui/Views/SettingsPage.xaml` (Appearance accent UI)
- `SqlDeployerGui/Converters/LogKindToBrushConverter.cs` (theme-aware log brushes)

## Open implementation risks

1. **Runtime accent refresh** (above) — spike first.
2. **Emerald/Amber contrast** — handled by per-preset `TextOnAccent`; verify the
   `AccentButton` pressed/hover states also read correctly.
3. **Mica + custom title bar** interaction — the app already extends into the title
   bar; confirm Mica draws behind it without a seam.
