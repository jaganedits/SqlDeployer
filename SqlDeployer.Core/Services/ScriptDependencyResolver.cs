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
}
