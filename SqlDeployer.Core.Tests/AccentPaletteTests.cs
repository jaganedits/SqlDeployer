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
