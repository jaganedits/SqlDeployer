# Dependency-Aware Deployment Ordering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the SQL deployer discover scripts recursively, run numbered phase folders in order, and within each folder auto-order tables so a parent table is created before any child that references it — plus live percentage progress and per-object success/failure reporting.

**Architecture:** A new pure, unit-testable `ScriptDependencyResolver` parses `CREATE TABLE` / `REFERENCES` from script text and topologically sorts each phase group. `SqlServerDeployer` gains recursive discovery (`DiscoverScripts`) and feeds the resolver, then tracks scripts by relative path. `DeploymentRunner` stops on first failure. `DeployViewModel` adds a percentage, an auto-order toggle, and a plan preview. The UI adds a progress bar and the toggle.

**Tech Stack:** .NET 10, C#, WinUI 3, CommunityToolkit.Mvvm, xUnit, Microsoft.Data.SqlClient.

**Spec:** `docs/superpowers/specs/2026-06-07-dependency-aware-deployment-ordering-design.md`

**Test command (from repo root):**
`dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~<Name>"`
Full build of the app: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj`

---

## File Structure

**Create**
- `SqlDeployer.Core/Services/ScriptDependencyResolver.cs` — types (`ScriptNode`, `DependencyEdge`, `OrderedPlan`) + parsing + ordering. Pure, no I/O.
- `SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs` — parser + ordering tests.
- `SqlDeployer.Core.Tests/ScriptDiscoveryTests.cs` — filesystem discovery tests (temp dirs).

**Modify**
- `SqlDeployer.Core/SqlServerDeployer.cs` — `DeploymentScript` record gains `RelativePath`; add `DiscoverScripts` / `PhaseOf`; rewrite `GetPendingScripts`; switch history identity to relative path in `GetDeployedScripts` / `ExecuteScript`.
- `SqlDeployer.Core/ISqlDeployer.cs` — add `autoOrder` param to `GetPendingScripts`.
- `SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs` — mirror the new `autoOrder` param.
- `SqlDeployer.Core/Services/DeploymentRunner.cs` — stop on first failure; phase-qualified display name; pass `autoOrder` through.
- `SqlDeployer.Core.Tests/DeploymentRunnerTests.cs` — update the "continues past failure" test to "stops on first failure".
- `SqlDeployer.Core/Models/AppSettings.cs` — add `AutoOrderByDependencies`.
- `SqlDeployer.Core/ViewModels/DeployViewModel.cs` — percentage props, toggle, plan preview, pass `autoOrder`.
- `SqlDeployerGui/Views/DeployPage.xaml` — progress bar + percentage text + auto-order toggle.

---

## Task 1: Resolver types + table-name normalization

**Files:**
- Create: `SqlDeployer.Core/Services/ScriptDependencyResolver.cs`
- Test: `SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs`

- [ ] **Step 1: Write the failing test**

Create `SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs`:

```csharp
using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class ScriptDependencyResolverTests
{
    [Theory]
    [InlineData("Customers", "dbo.customers")]
    [InlineData("dbo.Customers", "dbo.customers")]
    [InlineData("[dbo].[Customers]", "dbo.customers")]
    [InlineData("sales.Orders", "sales.orders")]
    [InlineData("[Function]", "dbo.function")]
    [InlineData("dbo . Customers", "dbo.customers")]
    public void Normalizes_table_names(string raw, string expected)
        => Assert.Equal(expected, ScriptDependencyResolver.NormalizeTableName(raw));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~Normalizes_table_names"`
Expected: FAIL to compile — `ScriptDependencyResolver` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `SqlDeployer.Core/Services/ScriptDependencyResolver.cs`:

```csharp
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~Normalizes_table_names"`
Expected: PASS (6 cases).

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/Services/ScriptDependencyResolver.cs SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs
git commit -m "feat(resolver): table-name normalization for FK matching"
```

---

## Task 2: Extract created & referenced tables (with comment/string stripping)

**Files:**
- Modify: `SqlDeployer.Core/Services/ScriptDependencyResolver.cs`
- Test: `SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `ScriptDependencyResolverTests`:

```csharp
[Fact]
public void Extracts_created_table_inside_if_not_exists_wrapper()
{
    var sql = @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
                BEGIN CREATE TABLE Users ( userid BIGINT PRIMARY KEY ); END";
    Assert.Equal(new[] { "dbo.users" }, ScriptDependencyResolver.CreatedTables(sql));
}

[Fact]
public void Extracts_bracketed_and_schema_qualified_created_table()
{
    Assert.Equal(new[] { "dbo.function" },
        ScriptDependencyResolver.CreatedTables("CREATE TABLE [dbo].[Function]( id INT );"));
}

[Fact]
public void Extracts_referenced_table_from_named_constraint_across_lines()
{
    var sql = @"CREATE TABLE planfeaturedetail (
        planid INT NOT NULL,
        CONSTRAINT fk_x FOREIGN KEY (planid)
            REFERENCES planmaster(planid) ON DELETE CASCADE );";
    Assert.Contains("dbo.planmaster", ScriptDependencyResolver.ReferencedTables(sql));
}

[Fact]
public void Ignores_references_inside_line_comments()
{
    var sql = "CREATE TABLE A ( id INT ); -- REFERENCES B";
    Assert.Empty(ScriptDependencyResolver.ReferencedTables(sql));
}

[Fact]
public void Ignores_references_inside_string_literals()
{
    var sql = "CREATE TABLE A ( note VARCHAR(50) DEFAULT 'see REFERENCES B for detail' );";
    Assert.Empty(ScriptDependencyResolver.ReferencedTables(sql));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~ScriptDependencyResolverTests"`
Expected: FAIL to compile — `CreatedTables` / `ReferencedTables` do not exist.

- [ ] **Step 3: Write minimal implementation**

Add these members to `ScriptDependencyResolver` (inside the class):

```csharp
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
        sql = BlockComment.Replace(sql, " ");
        sql = LineComment.Replace(sql, " ");
        sql = StringLiteral.Replace(sql, " '' ");
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~ScriptDependencyResolverTests"`
Expected: PASS (all Task 1 + Task 2 tests).

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/Services/ScriptDependencyResolver.cs SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs
git commit -m "feat(resolver): extract created/referenced tables, stripping comments and strings"
```

---

## Task 3: Topological ordering (Phase → Dependencies → Name) with cycle detection

**Files:**
- Modify: `SqlDeployer.Core/Services/ScriptDependencyResolver.cs`
- Test: `SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `ScriptDependencyResolverTests`:

```csharp
private static ScriptNode Node(string id, int phase, string sql) => new(id, phase, id, sql);

[Fact]
public void Orders_parent_before_child_regardless_of_name()
{
    var parent = Node("Planmaster.sql", 1, "CREATE TABLE Planmaster ( planid INT PRIMARY KEY );");
    var child = Node("PlanFeatureDetail.sql", 1,
        "CREATE TABLE planfeaturedetail ( planid INT, CONSTRAINT fk FOREIGN KEY (planid) REFERENCES planmaster(planid) );");

    var plan = ScriptDependencyResolver.Resolve(new[] { child, parent });

    var order = plan.Order.Select(n => n.Id).ToList();
    Assert.True(order.IndexOf("Planmaster.sql") < order.IndexOf("PlanFeatureDetail.sql"));
    Assert.Empty(plan.Cycle);
}

[Fact]
public void Phase_order_beats_dependencies_and_name()
{
    var table = Node("1.Table/Users.sql", 1, "CREATE TABLE Users ( userid INT PRIMARY KEY );");
    var alter = Node("2.Alter/Users.sql", 2, "ALTER TABLE Users ADD x INT;");

    var plan = ScriptDependencyResolver.Resolve(new[] { alter, table });

    Assert.Equal(new[] { "1.Table/Users.sql", "2.Alter/Users.sql" },
        plan.Order.Select(n => n.Id));
}

[Fact]
public void Independent_scripts_keep_stable_name_order()
{
    var a = Node("A.sql", 1, "CREATE TABLE A ( id INT );");
    var b = Node("B.sql", 1, "CREATE TABLE B ( id INT );");

    var plan = ScriptDependencyResolver.Resolve(new[] { b, a });

    Assert.Equal(new[] { "A.sql", "B.sql" }, plan.Order.Select(n => n.Id));
}

[Fact]
public void Detects_cycle_between_mutually_referencing_tables()
{
    var a = Node("A.sql", 1, "CREATE TABLE A ( id INT, CONSTRAINT f FOREIGN KEY (id) REFERENCES B(id) );");
    var b = Node("B.sql", 1, "CREATE TABLE B ( id INT, CONSTRAINT f FOREIGN KEY (id) REFERENCES A(id) );");

    var plan = ScriptDependencyResolver.Resolve(new[] { a, b });

    Assert.Contains("A.sql", plan.Cycle);
    Assert.Contains("B.sql", plan.Cycle);
}

[Fact]
public void Self_reference_is_not_a_cycle()
{
    var e = Node("Employees.sql", 1,
        "CREATE TABLE Employees ( id INT PRIMARY KEY, mgr INT, CONSTRAINT f FOREIGN KEY (mgr) REFERENCES Employees(id) );");

    var plan = ScriptDependencyResolver.Resolve(new[] { e });

    Assert.Empty(plan.Cycle);
    Assert.Single(plan.Order);
}

[Fact]
public void AutoOrder_false_uses_phase_then_name_only()
{
    var parent = Node("Planmaster.sql", 1, "CREATE TABLE Planmaster ( planid INT );");
    var child = Node("PlanFeatureDetail.sql", 1,
        "CREATE TABLE x ( planid INT, CONSTRAINT fk FOREIGN KEY (planid) REFERENCES planmaster(planid) );");

    var plan = ScriptDependencyResolver.Resolve(new[] { parent, child }, autoOrder: false);

    Assert.Equal(new[] { "PlanFeatureDetail.sql", "Planmaster.sql" },
        plan.Order.Select(n => n.Id)); // F < P by name; no dependency reordering
    Assert.Empty(plan.Edges);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~ScriptDependencyResolverTests"`
Expected: FAIL to compile — `Resolve` does not exist.

- [ ] **Step 3: Write minimal implementation**

Add to `ScriptDependencyResolver`:

```csharp
    // Order scripts: phase ascending (absolute), then a topological FK sort within
    // each phase (parent before child), then stable name order among independents.
    public static OrderedPlan Resolve(IEnumerable<ScriptNode> nodes, bool autoOrder = true)
    {
        var baseOrder = nodes
            .OrderBy(n => n.Phase)
            .ThenBy(n => n.NameKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!autoOrder)
            return new OrderedPlan(baseOrder, new List<DependencyEdge>(), new List<string>());

        // table -> first node that creates it
        var provider = new Dictionary<string, ScriptNode>();
        foreach (var n in baseOrder)
            foreach (var t in CreatedTables(n.Sql))
                provider.TryAdd(t, n);

        // parent -> child edges, only when both ends are in the same phase
        var edges = new List<DependencyEdge>();
        foreach (var child in baseOrder)
            foreach (var t in ReferencedTables(child.Sql))
                if (provider.TryGetValue(t, out var parent)
                    && !ReferenceEquals(parent, child)
                    && parent.Phase == child.Phase)
                    edges.Add(new DependencyEdge(parent.Id, child.Id, t));

        var ordered = new List<ScriptNode>();
        var cycle = new List<string>();

        foreach (var group in baseOrder.GroupBy(n => n.Phase).OrderBy(g => g.Key))
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~ScriptDependencyResolverTests"`
Expected: PASS (all resolver tests).

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/Services/ScriptDependencyResolver.cs SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs
git commit -m "feat(resolver): phase + topological ordering with cycle detection"
```

---

## Task 4: Recursive discovery + phase computation

**Files:**
- Modify: `SqlDeployer.Core/SqlServerDeployer.cs`
- Test: `SqlDeployer.Core.Tests/ScriptDiscoveryTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SqlDeployer.Core.Tests/ScriptDiscoveryTests.cs`:

```csharp
using System.IO;
using SqlDeployer;
using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class ScriptDiscoveryTests
{
    [Fact]
    public void PhaseOf_reads_leading_number_of_first_segment()
    {
        Assert.Equal(1, SqlServerDeployer.PhaseOf(Path.Combine("1.Table", "Users.sql")));
        Assert.Equal(5, SqlServerDeployer.PhaseOf(Path.Combine("5.Stored Procedure", "SP.sql")));
        Assert.Equal(int.MaxValue, SqlServerDeployer.PhaseOf("Loose.sql"));
    }

    [Fact]
    public void DiscoverScripts_recurses_and_assigns_phase_and_relative_id()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(root, "1.Table"));
        Directory.CreateDirectory(Path.Combine(root, "2.Alter"));
        File.WriteAllText(Path.Combine(root, "1.Table", "Users.sql"), "CREATE TABLE Users ( id INT );");
        File.WriteAllText(Path.Combine(root, "2.Alter", "Users.sql"), "ALTER TABLE Users ADD x INT;");

        var nodes = SqlServerDeployer.DiscoverScripts(root);

        Assert.Equal(2, nodes.Count);
        Assert.Contains(nodes, n => n.Id == Path.Combine("1.Table", "Users.sql") && n.Phase == 1);
        Assert.Contains(nodes, n => n.Id == Path.Combine("2.Alter", "Users.sql") && n.Phase == 2);

        Directory.Delete(root, true);
    }

    [Fact]
    public void Discover_plus_resolve_orders_the_real_fk_graph_correctly()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        var t = Path.Combine(root, "1.Table");
        Directory.CreateDirectory(t);
        File.WriteAllText(Path.Combine(t, "Users.sql"), "CREATE TABLE Users ( userid INT PRIMARY KEY );");
        File.WriteAllText(Path.Combine(t, "LoginHistory.sql"),
            "CREATE TABLE LoginHistory ( id INT, userid INT, CONSTRAINT f FOREIGN KEY (userid) REFERENCES Users(userid) );");
        File.WriteAllText(Path.Combine(t, "PlanMaster.sql"), "CREATE TABLE Planmaster ( planid INT PRIMARY KEY );");
        File.WriteAllText(Path.Combine(t, "PlanFeatureDetail.sql"),
            "CREATE TABLE planfeaturedetail ( planid INT, CONSTRAINT f FOREIGN KEY (planid) REFERENCES planmaster(planid) );");

        var plan = ScriptDependencyResolver.Resolve(SqlServerDeployer.DiscoverScripts(root));
        var order = plan.Order.Select(n => Path.GetFileName(n.Id)).ToList();

        Assert.True(order.IndexOf("Users.sql") < order.IndexOf("LoginHistory.sql"));
        Assert.True(order.IndexOf("PlanMaster.sql") < order.IndexOf("PlanFeatureDetail.sql"));
        Assert.Empty(plan.Cycle);

        Directory.Delete(root, true);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~ScriptDiscoveryTests"`
Expected: FAIL to compile — `DiscoverScripts` / `PhaseOf` do not exist.

- [ ] **Step 3: Write minimal implementation**

In `SqlDeployer.Core/SqlServerDeployer.cs`, add `using SqlDeployer.Services;` at the top (after the existing usings), and add these two public static methods inside the `SqlServerDeployer` class (e.g. just below the constructors):

```csharp
    // Leading integer of the first path segment ("1.Table" -> 1). Unnumbered
    // folders/files sort last (int.MaxValue).
    public static int PhaseOf(string relativePath)
    {
        var first = relativePath.Split(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        var digits = new string(first.TakeWhile(char.IsDigit).ToArray());
        return digits.Length > 0 ? int.Parse(digits) : int.MaxValue;
    }

    // Recursively find every *.sql under rootPath and build a ScriptNode for each:
    // Id = path relative to root (stable identity), Phase from the first folder,
    // NameKey = relative path (tertiary sort), Sql = file content.
    public static IReadOnlyList<ScriptNode> DiscoverScripts(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Scripts directory not found: {rootPath}");

        var nodes = new List<ScriptNode>();
        foreach (var file in Directory.GetFiles(rootPath, "*.sql", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(rootPath, file);
            nodes.Add(new ScriptNode(relative, PhaseOf(relative), relative, File.ReadAllText(file)));
        }
        return nodes;
    }
```

Note: `SqlServerDeployer.cs` already has `using System.Text.Json;` etc. but confirm `using System.IO;` is present — `Path`/`Directory`/`File` are used elsewhere in the file already, so it is. `System.Linq` is implicitly available via global usings (`TakeWhile`, `Select` are used elsewhere in the file).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~ScriptDiscoveryTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/SqlServerDeployer.cs SqlDeployer.Core.Tests/ScriptDiscoveryTests.cs
git commit -m "feat(deployer): recursive script discovery with phase computation"
```

---

## Task 5: Wire resolver into GetPendingScripts + relative-path identity

This task has no new unit test (it needs a live SQL Server, verified manually in Task 9). It must keep the existing fake-based tests compiling and passing.

**Files:**
- Modify: `SqlDeployer.Core/SqlServerDeployer.cs` (`DeploymentScript`, `GetPendingScripts`, `GetDeployedScripts`, `ExecuteScript`)
- Modify: `SqlDeployer.Core/ISqlDeployer.cs`
- Modify: `SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs`

- [ ] **Step 1: Add `RelativePath` to the `DeploymentScript` record**

In `SqlDeployer.Core/SqlServerDeployer.cs`, change the record (line 7):

```csharp
public record DeploymentScript(string FileName, string Version, bool IsRollback, string RelativePath = "");
```

- [ ] **Step 2: Add `autoOrder` to the interface**

In `SqlDeployer.Core/ISqlDeployer.cs`, change `GetPendingScripts`:

```csharp
    Task<List<DeploymentScript>> GetPendingScripts(
        string scriptsPath, string environment, string connectionString,
        CancellationToken cancellationToken = default,
        bool includeDeployed = false,
        bool autoOrder = true);
```

- [ ] **Step 3: Rewrite `GetPendingScripts` to use discovery + resolver**

In `SqlDeployer.Core/SqlServerDeployer.cs`, replace the whole `GetPendingScripts` method body with:

```csharp
    public async Task<List<DeploymentScript>> GetPendingScripts(
        string scriptsPath, string environment, string connectionString,
        CancellationToken cancellationToken = default,
        bool includeDeployed = false,
        bool autoOrder = true)
    {
        // Identity is the relative path; dedup against scripts already applied OK.
        var deployed = includeDeployed
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                await GetDeployedScripts(connectionString, cancellationToken),
                StringComparer.OrdinalIgnoreCase);

        var nodes = DiscoverScripts(scriptsPath); // throws DirectoryNotFoundException if missing
        var plan = ScriptDependencyResolver.Resolve(nodes, autoOrder);

        if (plan.Cycle.Count > 0)
            throw new InvalidOperationException(
                "Foreign-key cycle detected among: " + string.Join(", ", plan.Cycle) +
                ". Break the cycle (e.g. move one FK into an ALTER script).");

        var pending = new List<DeploymentScript>();
        foreach (var n in plan.Order)
        {
            var isRollback = Path.GetFileNameWithoutExtension(n.Id)
                .EndsWith("_rollback", StringComparison.OrdinalIgnoreCase);
            if (isRollback) continue;
            if (deployed.Contains(n.Id)) continue;

            var fullPath = Path.Combine(scriptsPath, n.Id);
            pending.Add(new DeploymentScript(fullPath, n.Id, isRollback, n.Id));
        }
        return pending;
    }
```

- [ ] **Step 4: Switch the deployed-set query to track by relative path (ScriptName column)**

In `SqlDeployer.Core/SqlServerDeployer.cs`, in `GetDeployedScripts`, change the `SELECT` so it returns the script identity stored in `ScriptName` (which now holds the relative path):

```csharp
                command.CommandText = @"
                    IF OBJECT_ID('dbo.DeploymentHistory', 'U') IS NOT NULL
                    BEGIN
                        SELECT DISTINCT ScriptName FROM dbo.DeploymentHistory WHERE Success = 1
                    END
                ";
```

(The reader still reads column 0 as a string — no other change in that method.)

- [ ] **Step 5: Store the relative-path identity in `ScriptName` when logging**

In `SqlDeployer.Core/SqlServerDeployer.cs`, in `ExecuteScript`, the call passes `version` (which the runner now supplies as the relative-path identity). Change the `LogDeployment` call (currently `LogDeployment(connection, Path.GetFileName(scriptPath), version, ...)`) so the **identity** goes into `ScriptName` and the leaf filename goes into `Version`:

```csharp
            // version == relative-path identity (from DeploymentScript.Version);
            // store it as ScriptName (NVARCHAR(255)); keep the leaf filename in Version.
            await LogDeployment(connection, version, Path.GetFileName(scriptPath),
                scriptSuccess, errorMessage, environment);
```

No change to the `LogDeployment` method signature or body is needed — it inserts `@scriptName` into `ScriptName` and `@version` into `Version`.

- [ ] **Step 6: Mirror the new param in the fake**

In `SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs`, update `GetPendingScripts` to accept `autoOrder` and record it:

```csharp
    // Records the last includeDeployed / autoOrder values so tests can assert the flags flow through.
    public bool? LastIncludeDeployed { get; private set; }
    public bool? LastAutoOrder { get; private set; }

    public Task<List<DeploymentScript>> GetPendingScripts(
        string scriptsPath, string environment, string connectionString,
        CancellationToken cancellationToken = default,
        bool includeDeployed = false,
        bool autoOrder = true)
    {
        LastIncludeDeployed = includeDeployed;
        LastAutoOrder = autoOrder;
        return Task.FromResult(new List<DeploymentScript>(Pending));
    }
```

(Replace the existing `LastIncludeDeployed` field declaration with the two-property block above, and replace the existing `GetPendingScripts` method.)

- [ ] **Step 7: Build and run the full Core test suite**

Run: `dotnet test SqlDeployer.Core.Tests`
Expected: PASS — all existing tests still green, resolver/discovery tests green. (The fake ignores ordering, so runner/VM tests are unaffected.)

- [ ] **Step 8: Commit**

```bash
git add SqlDeployer.Core/SqlServerDeployer.cs SqlDeployer.Core/ISqlDeployer.cs SqlDeployer.Core.Tests/Fakes/FakeSqlDeployer.cs
git commit -m "feat(deployer): order pending scripts via resolver; track by relative path"
```

---

## Task 6: Runner stops on first failure + phase-qualified names + autoOrder passthrough

**Files:**
- Modify: `SqlDeployer.Core/Services/DeploymentRunner.cs`
- Test: `SqlDeployer.Core.Tests/DeploymentRunnerTests.cs`

- [ ] **Step 1: Update the existing test to the new "stop on first failure" behavior**

In `SqlDeployer.Core.Tests/DeploymentRunnerTests.cs`, replace the `A_failing_script_is_counted_and_run_continues` test entirely with:

```csharp
    [Fact]
    public async Task Stops_at_the_first_failing_script()
    {
        var fake = new FakeSqlDeployer
        {
            Pending = { Script("001"), Script("002"), Script("003") },
            FailingVersions = { "002" }
        };
        var runner = new DeploymentRunner(fake);

        var result = await runner.RunAsync("cs", "path", "GUI",
            new Progress<DeploymentProgress>(), CancellationToken.None);

        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(2, fake.Executed.Count); // stopped after 002 failed; 003 not run
        Assert.Equal(new[] { "001", "002" }, fake.Executed.ToArray());
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~Stops_at_the_first_failing_script"`
Expected: FAIL — currently the runner continues, so `Executed.Count` is 3, not 2.

- [ ] **Step 3: Update the runner**

Replace the body of `RunAsync` in `SqlDeployer.Core/Services/DeploymentRunner.cs` with:

```csharp
    public async Task<DeploymentResult> RunAsync(
        string connectionString,
        string scriptsPath,
        string environment,
        IProgress<DeploymentProgress> progress,
        CancellationToken cancellationToken,
        bool force = false,
        bool autoOrder = true)
    {
        var pending = await _deployer.GetPendingScripts(
            scriptsPath, environment, connectionString, cancellationToken,
            includeDeployed: force, autoOrder: autoOrder);

        if (pending.Count == 0)
            return new DeploymentResult(0, 0, Cancelled: false, NoPendingScripts: true);

        int success = 0, failed = 0;

        for (int i = 0; i < pending.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return new DeploymentResult(success, failed, Cancelled: true, NoPendingScripts: false);

            var script = pending[i];
            var displayName = string.IsNullOrEmpty(script.RelativePath)
                ? Path.GetFileName(script.FileName)
                : script.RelativePath;

            progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName));

            try
            {
                await _deployer.ExecuteScript(
                    connectionString, script.FileName, script.Version, environment, cancellationToken);
                success++;
                progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName, Success: true));
            }
            catch (OperationCanceledException)
            {
                return new DeploymentResult(success, failed, Cancelled: true, NoPendingScripts: false);
            }
            catch (Exception ex)
            {
                failed++;
                progress.Report(new DeploymentProgress(i + 1, pending.Count, displayName, Success: false, Error: ex.Message));
                // Stop on first failure so a broken script can't cascade into a half-built DB.
                return new DeploymentResult(success, failed, Cancelled: false, NoPendingScripts: false);
            }
        }

        return new DeploymentResult(success, failed, Cancelled: false, NoPendingScripts: false);
    }
```

- [ ] **Step 4: Run the runner tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~DeploymentRunnerTests"`
Expected: PASS (including the rewritten test; the other runner tests are unaffected because they have ≤1 failure at the end).

- [ ] **Step 5: Commit**

```bash
git add SqlDeployer.Core/Services/DeploymentRunner.cs SqlDeployer.Core.Tests/DeploymentRunnerTests.cs
git commit -m "feat(runner): stop on first failure; report phase-qualified names; pass autoOrder"
```

---

## Task 7: ViewModel — percentage, auto-order toggle, plan preview

**Files:**
- Modify: `SqlDeployer.Core/Models/AppSettings.cs`
- Modify: `SqlDeployer.Core/ViewModels/DeployViewModel.cs`
- Test: `SqlDeployer.Core.Tests/DeployViewModelTests.cs`

- [ ] **Step 1: Add the setting**

In `SqlDeployer.Core/Models/AppSettings.cs`, add a property to `AppSettings`:

```csharp
    // When true, deploys auto-order scripts by detected foreign-key dependencies.
    public bool AutoOrderByDependencies { get; set; } = true;
```

- [ ] **Step 2: Write the failing tests**

Add to `SqlDeployer.Core.Tests/DeployViewModelTests.cs`:

```csharp
    [Fact]
    public async Task Deploy_drives_progress_percent_to_100()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer { Pending = { Script("001"), Script("002") } };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.Equal(100, vm.ProgressPercent);
        Assert.Contains("100%", vm.ProgressText);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void AutoOrder_toggle_persists_to_settings()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), "sqldeploy_vm_" + Guid.NewGuid().ToString("N") + ".json");
        var vm = NewVm(settings: new SettingsService(settingsPath));

        vm.AutoOrderByDependencies = false;

        Assert.False(new SettingsService(settingsPath).Load().AutoOrderByDependencies);
        File.Delete(settingsPath);
    }

    [Fact]
    public async Task Deploy_passes_autoOrder_flag_to_deployer()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var deployer = new FakeSqlDeployer { Pending = { Script("001") } };
        var vm = NewVm(deployer: deployer);
        vm.Server = "s"; vm.Database = "d"; vm.ScriptPath = tempDir;
        vm.AutoOrderByDependencies = false;

        await vm.DeployCommand.ExecuteAsync(null);

        Assert.False(deployer.LastAutoOrder);
        Directory.Delete(tempDir, true);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~DeployViewModelTests"`
Expected: FAIL to compile — `ProgressPercent`, `ProgressText`, `AutoOrderByDependencies` do not exist.

- [ ] **Step 4: Implement the ViewModel changes**

In `SqlDeployer.Core/ViewModels/DeployViewModel.cs`:

(a) Replace the `_progressValue` / `_progressMax` declarations (currently plain `[ObservableProperty]` fields) with versions that notify the computed properties:

```csharp
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _progressValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _progressMax = 1;

    public int ProgressPercent => ProgressMax > 0
        ? (int)(ProgressValue * 100.0 / ProgressMax)
        : 0;

    public string ProgressText => $"{ProgressPercent}% — {ProgressValue} / {ProgressMax}";

    // Auto-order deploys by detected FK dependencies (persisted). Default on.
    [ObservableProperty] private bool _autoOrderByDependencies = true;
```

(b) In the constructor, after `var loaded = _settings.Load();` (the existing line that loads settings), set the toggle field directly (no notification, no save loop):

```csharp
        _autoOrderByDependencies = loaded.AutoOrderByDependencies;
```

(c) Add a partial method (anywhere in the class) so toggling persists:

```csharp
    partial void OnAutoOrderByDependenciesChanged(bool value)
    {
        var s = _settings.Load();
        s.AutoOrderByDependencies = value;
        _settings.Save(s);
    }
```

(d) In `Deploy`, pass the flag to the runner. Change the `RunAsync` call:

```csharp
            var result = await _runner.RunAsync(cs, ScriptPath, Environment, progress, _cts.Token, ForceRerun, AutoOrderByDependencies);
```

(e) Add a best-effort plan preview. Add this method:

```csharp
    // Logs the computed run order (and detected parent->child links) before running,
    // so the auto-ordering is visible rather than a black box. Best-effort.
    private void LogPlanPreview()
    {
        if (!AutoOrderByDependencies) return;
        try
        {
            var nodes = SqlServerDeployer.DiscoverScripts(ScriptPath);
            var plan = ScriptDependencyResolver.Resolve(nodes, AutoOrderByDependencies);
            SuccessLog.Add(new LogEntry($"Plan: {plan.Order.Count} script(s), dependency-ordered.", LogKind.Info));
            foreach (var e in plan.Edges)
                SuccessLog.Add(new LogEntry($"  {e.ChildId} depends on {e.ParentId} (table {e.Table})", LogKind.Info));
        }
        catch
        {
            // Preview is best-effort; the real run still reports errors.
        }
    }
```

Then call it inside `Deploy`, immediately after `Status = "Loading pending scripts...";`:

```csharp
        LogPlanPreview();
```

Confirm the file's `using` block includes `using SqlDeployer;` and `using SqlDeployer.Services;` (both are already present in this file).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test SqlDeployer.Core.Tests --filter "FullyQualifiedName~DeployViewModelTests"`
Expected: PASS (new tests + all existing VM tests).

- [ ] **Step 6: Commit**

```bash
git add SqlDeployer.Core/Models/AppSettings.cs SqlDeployer.Core/ViewModels/DeployViewModel.cs SqlDeployer.Core.Tests/DeployViewModelTests.cs
git commit -m "feat(vm): progress percentage, auto-order toggle, plan preview"
```

---

## Task 8: UI — progress bar, percentage label, auto-order toggle

No unit test (XAML). Verified by build + the manual smoke in Task 9.

**Files:**
- Modify: `SqlDeployerGui/Views/DeployPage.xaml`

- [ ] **Step 1: Add the progress UI and toggle**

In `SqlDeployerGui/Views/DeployPage.xaml`, inside the left `StackPanel` (the one with `Spacing="16"`), add a progress block **immediately after** the primary-actions `<Grid>` (the one containing the Test/Deploy/Cancel buttons, which ends with its `</Grid>` near line 146) and **before** the "Re-run option" `<StackPanel>`:

The progress bar is always present (it shows 0% when idle). WinUI 3 has **no**
implicit bool→Visibility conversion, so do **not** bind `Visibility` to `IsBusy`;
keeping the bar always visible avoids needing a converter.

```xml
                    <!-- Live progress: bar + percentage -->
                    <StackPanel Spacing="6">
                        <ProgressBar Minimum="0"
                                     Maximum="{Binding ProgressMax}"
                                     Value="{Binding ProgressValue}" />
                        <TextBlock Text="{Binding ProgressText}"
                                   Style="{StaticResource Subtle}" FontSize="12" />
                    </StackPanel>
```

Then, inside the existing "Re-run option" `<StackPanel Spacing="2">`, add the auto-order toggle as a second checkbox (after the existing "Re-run already-deployed scripts" `CheckBox` and its description `TextBlock`):

```xml
                        <CheckBox Content="Auto-order scripts by foreign keys"
                                  IsChecked="{Binding AutoOrderByDependencies, Mode=TwoWay}"
                                  IsEnabled="{Binding IsIdle}" Margin="0,8,0,0" />
                        <TextBlock Text="Detects parent/child table dependencies and runs parents first. Turn off to run in folder + name order only."
                                   Style="{StaticResource Subtle}" FontSize="12"
                                   TextWrapping="Wrap" />
```

- [ ] **Step 2: Build the app**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj`
Expected: Build succeeded (0 errors). Fix any XAML binding/markup errors reported.

- [ ] **Step 3: Commit**

```bash
git add SqlDeployerGui/Views/DeployPage.xaml
git commit -m "feat(ui): progress bar with percentage and auto-order toggle on Deploy page"
```

---

## Task 9: Full build, full test run, manual smoke

**Files:** none (verification only).

- [ ] **Step 1: Run the entire test suite**

Run: `dotnet test SqlDeployer.Core.Tests`
Expected: PASS — all tests (resolver, discovery, runner, view model).

- [ ] **Step 2: Build the whole app**

Run: `dotnet build SqlDeployerGui/SqlDeployerGui.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Manual smoke against the real scripts**

Launch the app, go to the Deploy page, and point the Script path at
`C:\Users\Jagan\Downloads\Dev_Branch\Database` (and separately at
`...\Database\1.Table`). Connect to a scratch/dev database and Deploy.

Verify:
- The **plan preview** lists detected links (e.g. `1.Table\LoginHistory.sql depends on 1.Table\Users.sql`).
- Tables deploy in dependency order — no "invalid object name" / FK errors for
  `LoginHistory` (needs `Users`), `PlanFeatureDetail`/`PlanAmountDetail`/`PlanHistory`
  (need `Planmaster`), `ForumRegistration` (needs `Forums`), `MemberTier*` (need `MemberTiers`).
- The **progress bar + percentage** advance and reach 100%.
- The **Success** tab lists each object as `1.Table\X :: ...`; on an induced failure
  (e.g. a deliberately broken script), the **Errors** tab shows the object + SQL
  error and the run **stops** there.
- Toggling **Auto-order scripts by foreign keys** off and on persists across an
  app restart.

- [ ] **Step 4: Final commit (if any tweaks were needed)**

```bash
git add -A
git commit -m "chore: dependency-aware ordering verification fixes"
```

---

## Self-Review Notes

- **Spec coverage:** recursion + phase order (T4/T5), FK topo sort + normalization + comment/string stripping + self-ref + cycle (T1–T3), relative-path identity / `Users.sql` collision fix (T5), stop-on-first-failure (T6), live percentage + per-object success/error + plan preview + toggle (T7/T8). Security fixes are explicitly out of scope per the spec.
- **Types are consistent across tasks:** `ScriptNode(Id, Phase, NameKey, Sql)`, `DependencyEdge(ParentId, ChildId, Table)`, `OrderedPlan(Order, Edges, Cycle)`, `DeploymentScript(FileName, Version, IsRollback, RelativePath)`, `GetPendingScripts(..., includeDeployed, autoOrder)`, `RunAsync(..., force, autoOrder)`.
- **Known boundary:** existing `DeploymentHistory` rows written before this change store the leaf filename in `ScriptName`; after the switch to relative-path identity, previously-deployed scripts may be re-evaluated once. Because the real table scripts are guarded with `IF NOT EXISTS`, re-running is a no-op — safe.
