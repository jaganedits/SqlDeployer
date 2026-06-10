using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v2.3.4", "2.3.4.0")]
    [InlineData("2.3.4", "2.3.4.0")]
    [InlineData("V10.0.1", "10.0.1.0")]
    [InlineData("v2.4", "2.4.0.0")]
    public void ParseTag_accepts_common_tag_shapes(string tag, string expected)
        => Assert.Equal(Version.Parse(expected), UpdateCheck.ParseTag(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v2.3.4-beta")]
    public void ParseTag_rejects_non_versions(string? tag)
        => Assert.Null(UpdateCheck.ParseTag(tag));

    [Theory]
    [InlineData("2.3.3", "2.3.2", true)]
    [InlineData("2.3.2", "2.3.2", false)]
    [InlineData("2.3.1", "2.3.2", false)]
    [InlineData("2.4", "2.3.9", true)]
    public void IsNewer_compares_normalized_versions(string candidate, string current, bool expected)
        => Assert.Equal(expected,
            UpdateCheck.IsNewer(Version.Parse(candidate), Version.Parse(current)));

    [Fact]
    public void ParseLatestRelease_reads_tag_and_url()
    {
        const string json = """
            { "tag_name": "v2.3.2", "html_url": "https://github.com/jaganedits/SqlDeployer/releases/tag/v2.3.2", "name": "SqlDeployer 2.3.2" }
            """;

        var release = UpdateCheck.ParseLatestRelease(json);

        Assert.NotNull(release);
        Assert.Equal("v2.3.2", release!.Value.Tag);
        Assert.Equal("https://github.com/jaganedits/SqlDeployer/releases/tag/v2.3.2", release.Value.Url);
    }

    [Fact]
    public void ParseLatestRelease_returns_null_without_tag()
        => Assert.Null(UpdateCheck.ParseLatestRelease("""{ "message": "Not Found" }"""));
}
