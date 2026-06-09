using System.Text.RegularExpressions;

namespace SqlDeployer.Services;

// A script to be ordered. Id is the stable identity (relative path) and doubles
// as the tertiary (name) sort key; Phase is the numeric folder prefix
// (int.MaxValue when unnumbered); Sql is the file content.
public sealed record ScriptNode(string Id, int Phase, string Sql);

// A detected "parent table must be created before child" relationship.
public sealed record DependencyEdge(string ParentId, string ChildId, string Table);

// Result of ordering: final order, the edges found, and any cycle members
// (empty when the graph is a DAG).
public sealed record OrderedPlan(
    IReadOnlyList<ScriptNode> Order,
    IReadOnlyList<DependencyEdge> Edges,
    IReadOnlyList<string> Cycle);

// The kind of object a script creates, in dependency order: a lower value must be
// deployed before a higher one. This is the *primary* ordering key — it makes the
// canonical order partition-infra → sequence → function → table → alter → index →
// view → procedure → trigger → data, regardless of how the author numbers folders.
public enum SqlObjectKind
{
    PartitionInfra = 0, // filegroups, partition functions/schemes (ALTER DATABASE)
    Sequence = 1,
    Function = 2,       // tables' computed columns / defaults call these
    Table = 3,
    AlterTable = 4,     // ADD COLUMN / ADD CONSTRAINT (all tables exist by now)
    Index = 5,
    View = 6,
    Procedure = 7,
    Trigger = 8,
    Data = 9,           // INSERT / UPDATE / MERGE seed scripts
    Unknown = 10,       // nothing recognized — ordered last, author phase breaks ties
}

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

    // Ordered (kind, pattern) probes. The first match against the noise-stripped SQL
    // wins, so a multi-statement file classifies by its most significant DDL — e.g. a
    // CREATE TABLE followed by ALTER TABLE is a Table, and a table-valued function
    // (CREATE FUNCTION … RETURNS TABLE) is a Function, not a Table.
    private static readonly (SqlObjectKind Kind, Regex Rx)[] KindProbes =
    [
        // Database-level partition infrastructure runs first. ALTER DATABASE covers the
        // filegroup/.ndf scripts; the partition function/scheme have their own keywords.
        (SqlObjectKind.PartitionInfra,
            new(@"\bCREATE\s+PARTITION\s+(FUNCTION|SCHEME)\b|\bALTER\s+DATABASE\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (SqlObjectKind.Sequence, new(@"\bCREATE\s+SEQUENCE\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (SqlObjectKind.Function, new(@"\bCREATE\s+(OR\s+ALTER\s+)?FUNCTION\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (SqlObjectKind.Table, new(@"\bCREATE\s+TABLE\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (SqlObjectKind.AlterTable, new(@"\bALTER\s+TABLE\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (SqlObjectKind.Index,
            new(@"\bCREATE\s+(UNIQUE\s+)?(CLUSTERED\s+|NONCLUSTERED\s+)?INDEX\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (SqlObjectKind.View, new(@"\bCREATE\s+(OR\s+ALTER\s+)?VIEW\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (SqlObjectKind.Procedure, new(@"\bCREATE\s+(OR\s+ALTER\s+)?PROC(EDURE)?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (SqlObjectKind.Trigger, new(@"\bCREATE\s+(OR\s+ALTER\s+)?TRIGGER\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        (SqlObjectKind.Data, new(@"\b(INSERT|UPDATE|MERGE)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ];

    // Classify a script by what it creates, from its SQL content (comments/strings
    // stripped). Used as the primary ordering key in Resolve.
    public static SqlObjectKind ClassifyKind(string sql)
    {
        var clean = StripNoise(sql);
        foreach (var (kind, rx) in KindProbes)
            if (rx.IsMatch(clean))
                return kind;
        return SqlObjectKind.Unknown;
    }

    // Order scripts: object-type rank ascending (absolute — a function before a table
    // that uses it, etc.), then folder phase, then a topological FK sort within each
    // rank (parent before child), then stable name order among independents.
    public static OrderedPlan Resolve(IEnumerable<ScriptNode> nodes, bool autoOrder = true)
    {
        var all = nodes.ToList();

        // Escape hatch: literal folder-phase + name order, no ranking or FK sort.
        if (!autoOrder)
            return new OrderedPlan(
                all.OrderBy(n => n.Phase)
                   .ThenBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
                   .ToList(),
                [], []);

        // Classify each script once; rank is the primary ordering key.
        var rank = all.ToDictionary(n => n, n => (int)ClassifyKind(n.Sql));

        var baseOrder = all
            .OrderBy(n => rank[n])
            .ThenBy(n => n.Phase)
            .ThenBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // table -> first node that creates it
        var provider = new Dictionary<string, ScriptNode>();
        foreach (var n in baseOrder)
            foreach (var t in CreatedTables(n.Sql))
                provider.TryAdd(t, n);

        // parent -> child edges, only when both ends share the same object-type rank
        // (so the FK topo-sort runs within the Table rank, where CREATE/REFERENCES live)
        var edges = new List<DependencyEdge>();
        foreach (var child in baseOrder)
            foreach (var t in ReferencedTables(child.Sql))
                if (provider.TryGetValue(t, out var parent)
                    && !ReferenceEquals(parent, child)
                    && rank[parent] == rank[child])
                    edges.Add(new DependencyEdge(parent.Id, child.Id, t));

        var ordered = new List<ScriptNode>();
        var cycle = new List<string>();

        foreach (var group in baseOrder.GroupBy(n => rank[n]).OrderBy(g => g.Key))
        {
            var groupNodes = group.ToList(); // already in baseOrder within the group
            var ids = groupNodes.Select(n => n.Id).ToHashSet();
            var indegree = groupNodes.ToDictionary(n => n.Id, _ => 0);
            var childrenOf = groupNodes.ToDictionary(n => n.Id, _ => new List<string>());

            foreach (var e in edges)
                if (ids.Contains(e.ParentId) && ids.Contains(e.ChildId))
                {
                    indegree[e.ChildId]++;
                    childrenOf[e.ParentId].Add(e.ChildId);
                }

            var emitted = new HashSet<string>();
            bool progressed = true;
            while (emitted.Count < groupNodes.Count && progressed)
            {
                progressed = false;
                foreach (var n in groupNodes) // scan in baseOrder; emit earliest ready
                {
                    if (emitted.Contains(n.Id) || indegree[n.Id] != 0) continue;
                    ordered.Add(n);
                    emitted.Add(n.Id);
                    foreach (var c in childrenOf[n.Id]) indegree[c]--;
                    progressed = true;
                    break; // restart scan to keep baseOrder priority among ready nodes
                }
            }

            cycle.AddRange(groupNodes.Where(n => !emitted.Contains(n.Id)).Select(n => n.Id));
        }

        // On a cycle, return a safe phase+name order and report the offenders.
        return cycle.Count > 0
            ? new OrderedPlan(baseOrder, edges, cycle)
            : new OrderedPlan(ordered, edges, cycle);
    }
}
