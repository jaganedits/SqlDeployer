using System.Text.RegularExpressions;

namespace SqlDeployer.Services;

// A script to be ordered. Id is the stable identity (relative path);
// Phase is the numeric folder prefix (int.MaxValue when unnumbered);
// NameKey is the tertiary sort; Sql is the file content.
public sealed record ScriptNode(string Id, int Phase, string NameKey, string Sql);

// A detected "parent table must be created before child" relationship.
public sealed record DependencyEdge(string ParentId, string ChildId, string Table);

// Result of ordering: final order, the edges found, and any cycle members
// (empty when the graph is a DAG).
public sealed record OrderedPlan(
    IReadOnlyList<ScriptNode> Order,
    IReadOnlyList<DependencyEdge> Edges,
    IReadOnlyList<string> Cycle);

// Pure SQL-text analysis + topological ordering. No file or DB access.
public static class ScriptDependencyResolver
{
    // Normalize a raw table token to "schema.table", lowercase, brackets/quotes
    // stripped, schema defaulted to dbo. So [dbo].[X], dbo.X and X all unify.
    public static string NormalizeTableName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Table name must not be empty.", nameof(raw));

        var cleaned = raw.Replace("[", "").Replace("]", "").Replace("\"", "").Trim();
        var parts = cleaned.Split('.',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Qualified names use the LAST two segments: [db.]schema.table -> schema.table.
        // The table is always the final segment; the schema the one before it.
        var schema = parts.Length >= 2 ? parts[^2] : "dbo";
        var table = parts.Length >= 1 ? parts[^1] : cleaned;

        return (schema + "." + table).ToLowerInvariant();
    }

    private static readonly Regex BlockComment =
        new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex LineComment =
        new(@"--[^\n]*", RegexOptions.Compiled);
    private static readonly Regex StringLiteral =
        new(@"'(?:[^']|'')*'", RegexOptions.Compiled);

    private static readonly Regex CreateTableRx = new(
        @"CREATE\s+TABLE\s+(\[?[A-Za-z0-9_]+\]?(?:\s*\.\s*\[?[A-Za-z0-9_]+\]?)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReferencesRx = new(
        @"REFERENCES\s+(\[?[A-Za-z0-9_]+\]?(?:\s*\.\s*\[?[A-Za-z0-9_]+\]?)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Remove block comments, line comments, and string literals so keywords and
    // table names inside them can't create false matches.
    public static string StripNoise(string sql)
    {
        // Strip in quoting-precedence order: string literals are outermost (comment
        // markers inside a string are literal text), then block, then line comments.
        sql = StringLiteral.Replace(sql, " '' ");
        sql = BlockComment.Replace(sql, " ");
        sql = LineComment.Replace(sql, " ");
        return sql;
    }

    public static IReadOnlyList<string> CreatedTables(string sql) =>
        CreateTableRx.Matches(StripNoise(sql))
            .Select(m => NormalizeTableName(m.Groups[1].Value))
            .Distinct()
            .ToList();

    public static IReadOnlyList<string> ReferencedTables(string sql) =>
        ReferencesRx.Matches(StripNoise(sql))
            .Select(m => NormalizeTableName(m.Groups[1].Value))
            .Distinct()
            .ToList();
}
