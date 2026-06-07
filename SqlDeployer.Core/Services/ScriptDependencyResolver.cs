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
        var cleaned = raw.Replace("[", "").Replace("]", "").Replace("\"", "").Trim();
        var parts = cleaned.Split('.',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string schema, table;
        if (parts.Length >= 2) { schema = parts[0]; table = parts[1]; }
        else { schema = "dbo"; table = parts.Length == 1 ? parts[0] : cleaned; }

        return (schema + "." + table).ToLowerInvariant();
    }
}
