using System.Text.Json;

namespace SqlDeployer.Services;

// Pure logic for the GitHub-release update fallback, kept UI- and network-free
// so it is unit-testable: parse a release tag ("v2.3.4" / "2.3.4") and decide
// whether it is newer than the running version.
public static class UpdateCheck
{
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var trimmed = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var v) ? Normalize(v) : null;
    }

    // GitHub "latest release" JSON -> (tag_name, html_url). Null when the payload
    // has no usable tag (e.g. a "Not Found" error body). Invalid JSON throws —
    // callers treat any exception as a failed check.
    public static (string Tag, string? Url)? ParseLatestRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tag_name", out var tagElement)) return null;
        var tag = tagElement.GetString();
        if (string.IsNullOrWhiteSpace(tag)) return null;

        string? url = doc.RootElement.TryGetProperty("html_url", out var urlElement)
            ? urlElement.GetString()
            : null;
        return (tag, url);
    }

    public static bool IsNewer(Version candidate, Version current)
        => Normalize(candidate) > Normalize(current);

    // Version treats missing parts as -1, which breaks comparisons (2.4 < 2.4.0);
    // normalize them to 0 so 2.4 == 2.4.0.0.
    private static Version Normalize(Version v) => new(
        v.Major, v.Minor, v.Build < 0 ? 0 : v.Build, v.Revision < 0 ? 0 : v.Revision);
}
