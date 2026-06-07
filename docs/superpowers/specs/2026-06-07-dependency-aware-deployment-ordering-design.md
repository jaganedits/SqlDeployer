# Dependency-Aware Deployment Ordering — Design

- **Date:** 2026-06-07
- **Status:** Approved (pending final spec review)
- **Component:** `SqlDeployer.Core` (engine) + `SqlDeployerGui` (UI)

## Problem

When deploying SQL scripts, a child table whose `CREATE TABLE` contains an inline
foreign key fails if it runs **before** the parent table it references. Today the
tool runs scripts in filename order, which does not respect these relationships.

This is not a rare edge case in the real database. Against the actual scripts in
`Dev_Branch\Database\1.Table`, **7 of the 8 declared foreign keys break under the
current alphabetical order**:

| Child script | references parent | breaks alphabetically? |
|---|---|---|
| `LoginHistory` | `Users` | yes (`L` < `U`) |
| `PlanFeatureDetail` | `Planmaster` | yes (`F` < `M`) |
| `PlanAmountDetail` | `Planmaster` | yes (`A` < `M`) |
| `PlanHistory` | `Planmaster` | yes (`H` < `M`) |
| `ForumRegistration` | `Forums` | yes (`R` < `s`) |
| `ForumRegistrationFeatureDetail` | `ForumRegistration` | no |
| `MemberTierAmountDetail` | `MemberTiers` | yes |
| `MemberTiersFeatureDetail` | `MemberTiers` | no |

A naming-convention fix was rejected: the team cannot rely on every author
numbering files correctly. **The tool must detect the dependencies itself.**

## Goals

1. Automatically order scripts so a referenced (parent) table is created before
   any table that references it (child), based on the SQL content — no naming
   convention required.
2. Support the real folder layout: numbered phase folders run in order
   (`1.Table` → `2.Alter` → `3.Function` → `4.Insert Script` → `5.Stored Procedure`).
3. Support both usage modes:
   - Point at the **root** (`...\Database`) → run all phases in order.
   - Point at a **single folder** (`...\Database\1.Table`) → run just that folder.
4. Be **transparent**: show the computed order (and detected parent→child links)
   before running, so the automatic ordering is never a black box.
5. Be **safe**: stop on first failure; detect dependency cycles; provide an
   on/off escape hatch.
6. Improve progress/result reporting: live completion percentage and a per-object
   success/failure list (table / function / SP) with the SQL error on failure.

## Non-Goals (deferred to a later change)

- The security/correctness fixes from the earlier code review
  (`Encrypt=false`, the swallowed-`SqlException` re-run bug). Tracked separately.
- Rollback orchestration.
- Parsing dependencies other than foreign keys (e.g. SP→table, view→table).
  Cross-object-type ordering is handled by **phase order**, not parsing.

## Current Behavior (and why it breaks here)

`SqlServerDeployer.GetPendingScripts` (`SqlDeployer.Core/SqlServerDeployer.cs`):

- Calls `Directory.GetFiles(scriptsPath, "*.sql")` — **top-level only**, so the
  phased subfolders are invisible. Pointed at `...\Database`, it finds **zero**
  scripts.
- Orders by a numeric "version" extracted from the filename prefix. The real
  files have no numeric prefix, so the version collapses to the name. This causes
  two further problems:
  - **No FK ordering** — purely lexical.
  - **Identity collision** — `1.Table\Users.sql` and `2.Alter\Users.sql` both
    reduce to version `"Users"`, so the Alter script is treated as
    "already deployed" and **silently skipped**.

## Design

### Overview — one rule for both modes

Whether the user selects the root or a single folder, the engine applies the same
rule:

> **recurse → group scripts by their folder → order folders by leading number →
> topologically FK-sort the scripts inside each folder → concatenate.**

Selecting the root yields five folder groups; selecting `1.Table` yields one. Same
code path.

### Ordering: Phase → Dependencies → Name

The final order is determined by three keys, in priority:

1. **Phase (primary).** The leading integer of the script's folder name relative
   to the selected root. `1.Table` = 1, `2.Alter` = 2, … Folders without a leading
   number sort after numbered ones, by name. Phase order is **absolute** — it is
   never overridden by detected dependencies.
2. **Dependencies (secondary).** Within a single phase group, a topological sort
   places each parent table before its children. Only edges where **both**
   endpoints are in the same phase group are considered (a reference to a table
   created in an earlier phase, or already present in the DB, imposes no
   intra-phase constraint).
3. **Name (tertiary).** Among scripts with no dependency relationship, a stable
   filename sort keeps the order predictable run-to-run.

### FK detection (`ScriptDependencyResolver` — new, pure, testable)

For each script the resolver computes:

- **Provides** — the set of tables it creates: matches `CREATE TABLE <name>`.
- **Requires** — the set of tables it references: matches `REFERENCES <name>`
  (covers inline column FKs, named table-level constraints, and
  `ALTER TABLE … ADD … FOREIGN KEY … REFERENCES …`).

**Preprocessing before matching:**

- Strip `--` line comments and `/* … */` block comments.
- Strip single-quoted string literals (so a literal containing the word
  `REFERENCES` cannot create a false edge; also neutralizes the table name inside
  `IF NOT EXISTS (… WHERE name = 'X')`).

**Name normalization** (so references resolve across stylistic differences seen in
the real scripts):

- Strip `[ ]` brackets and `" "` quotes.
- Default an unqualified name to schema `dbo`; compare on `schema.table`.
- Lowercase. Real examples this must unify: `REFERENCES planmaster` ↔
  `CREATE TABLE Planmaster`; `CREATE TABLE dbo.ForumRegistration` ↔
  `REFERENCES ForumRegistration`; `CREATE TABLE [dbo].[Function]`.

**Edge construction:** build a map `normalized table name → script that provides
it`. For each script, for each required table that is provided **by another script
in the same phase group**, add edge `parent → child`. **Self-references**
(a table whose FK targets itself) are ignored. References to tables not in the set
are ignored.

### Topological sort

- Kahn's algorithm over each phase group.
- Among nodes with in-degree 0, pick the one with the smallest **name key** (the
  tertiary sort), so output is deterministic and stable.
- **Cycle detection:** if not all nodes are emitted, a cycle exists (e.g. two
  tables with mutual inline FKs — unsatisfiable by ordering). Report the involved
  scripts clearly and abort before executing anything (the user must break the
  cycle, e.g. move one FK to an `ALTER`).

### Script identity / tracking

A script's identity becomes its **path relative to the selected root**
(e.g. `1.Table\Users.sql`), not a parsed version. This fixes the
`Users.sql` collision. The `DeploymentHistory` table records this relative path,
and pending-detection dedups on it.

### Recursion / discovery

- Enumerate `*.sql` recursively under the selected path (non-`.sql` files such as
  `EF_02_04.bak` are ignored).
- Compute each file's phase from its first path segment relative to the root.

### Progress & reporting (UI)

Most of this exists in `DeployViewModel`; the additions are small.

- **Live percentage:** a text indicator `60% — 3 / 5` next to the existing bar,
  plus the current item: `Running 1.Table\LoginHistory… (60%)`. Percentage =
  `current / total` across **all** scripts in the run.
- **Success list:** each object that deployed OK, labeled with its phase:
  `1.Table\Users :: OK`.
- **Error list:** each failed object with the SQL error:
  `5.Stored Procedure\SP_USERS :: Invalid column name 'x'`. The **Errors (n)**
  tab header shows the failed count.
- **Stop on first failure:** `DeploymentRunner` halts at the first failed script
  (instead of continuing), so the error list pinpoints the blocking script rather
  than a cascade.
- **End summary:** `Finished: 23 succeeded, 1 failed` (existing, retained).

### Transparency & safety

- Before execution, the engine writes the computed plan to the output log: the
  phase order and, within `1.Table`, the detected `parent → child` links.
- **Toggle** `Auto-order by dependencies` (default **on**), persisted in settings.
  When off, scripts run in phase + name order only — an escape hatch if the parser
  ever mishandles exotic SQL.

## Components

**New**

- `SqlDeployer.Core/Services/ScriptDependencyResolver.cs` — pure functions:
  parse provides/requires, normalize names, build groups, topo-sort, detect
  cycles. No I/O, fully unit-testable.
- `SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs` — see Testing.

**Changed**

- `SqlServerDeployer.cs` — recursive discovery; use the resolver for ordering;
  track/dedup by relative path; `CreateDeploymentTrackingTable`/`LogDeployment`
  store the relative path.
- `Services/DeploymentRunner.cs` — stop on first failure; report phase-qualified
  names; surface the plan/cycle result.
- `ViewModels/DeployViewModel.cs` — percentage property, current-item text,
  `AutoOrderByDependencies` toggle, plan preview into the log.
- `Models/DeploymentProgress.cs` / `DeploymentResult.cs` — carry percentage /
  cycle info as needed.
- `SqlDeployerGui/Views/DeployPage.xaml` — show the percentage label and the
  toggle.

## Data Flow

```
DeployPage (Deploy click)
  → DeployViewModel.Deploy
    → DeploymentRunner.RunAsync(root path, …)
      → SqlServerDeployer.GetPendingScripts
          • recurse *.sql
          • read content
          • ScriptDependencyResolver.Order(scripts)   ← phase + FK topo + name
          • filter already-deployed (by relative path)
      → (log the computed plan)
      → foreach ordered script: ExecuteScript  (stop on first failure)
          • IProgress<DeploymentProgress> → OnProgress → bar %, success/error lists
```

## Error Handling

- **Cycle detected:** abort before any execution; list the scripts in the cycle.
- **Unparseable / no CREATE TABLE:** the script still deploys; it simply
  contributes no edges (treated as a leaf, ordered by name within its phase).
- **Reference to unknown table:** no edge; if it truly doesn't exist at runtime,
  the SQL error surfaces in the error list and the run stops there.
- **Missing folder / no scripts:** existing validation messaging is retained.

## Testing Strategy (TDD — write tests first)

`ScriptDependencyResolver` is pure, so the parsing/ordering is covered without a
database. Cases (each from a real or realistic snippet):

- Inline column FK: `cust_id INT REFERENCES Customers(id)`.
- Named table-level constraint spanning lines (the `Employees`/`Departments` and
  `planfeaturedetail`/`planmaster` shapes).
- Bracketed / schema-qualified names: `[dbo].[Customers]`, `dbo.Customers`,
  unqualified `Customers` — all resolve to the same table.
- Case differences: `planmaster` ↔ `Planmaster`.
- Comment containing `REFERENCES` is ignored.
- String literal containing a table name (`WHERE name = 'X'`) creates no edge.
- Self-reference is ignored (no false cycle).
- Multiple `CREATE TABLE` in one file.
- Reference to a table outside the script set → no edge.
- A full `1.Table` fixture reproducing the 8 real FKs → asserts every parent
  precedes its child.
- Cycle (A↔B) → detected and reported.
- Phase ordering: a root with `1.Table` + `2.Alter` → all phase-1 before phase-2.
- Independent scripts → stable name order.

UI/runner: `DeploymentRunnerTests` extended for stop-on-first-failure and
percentage reporting.

## Assumptions

- One `CREATE TABLE` per file in the current repo (multiple is still supported).
- FKs only reference tables within the same phase group (`1.Table`). True today.
- SQL Server identifier semantics (case-insensitive, `dbo` default schema).
