# Semantic Object-Type Deployment Ordering — Design

- **Date:** 2026-06-09
- **Status:** Approved
- **Component:** `SqlDeployer.Core` (`ScriptDependencyResolver`)

## Problem

Deploying a real database (`E:\TFS\Amutha_Surabhi\Dev_Branch\DBScript`) from scratch
produces ~300 cascading errors. Root cause: the deployer orders scripts by the
**leading digit of the first folder segment** (`SqlServerDeployer.PhaseOf`), then by
relative-path name, then an FK topo-sort *within* a phase. It has **no notion of
cross-object-type dependencies** (table→function, table→partition scheme,
view→function); the previous design explicitly deferred that, relying on phase-folder
order instead.

But that database's folders are numbered in the wrong order for a from-scratch build:

| Folder | Phase the tool assigns | Consequence |
|---|---|---|
| `5. Function` | 5 | Functions run *after* tables(1)/views(2)/SPs(3) that call them |
| `1. Tables\1. Partitioning Script` | 1, sorts after `1. Create` | `"1. Create"` < `"1. Partitioning Script"`, so partition schemes are created *after* the tables that use them |
| `11.Partition_Script` | 11 | Even later |

Observed error families, all tracing to those two inversions:

1. `dbo.FN_GETMETASUBDESC` / `F_ACCOUNTINGYEAR` / `REPLACEASCII` "could not be found"
   — tables/alters/views/SPs call UDFs that don't exist yet (functions are phase 5).
2. `Invalid partition scheme 'PartitionBymonth'` — partition fn/scheme created after
   the partitioned tables.
3. `Invalid column name 'SHOPTYPE'/'ECCATEGORYNAME'…` — `2. Alter` scripts that *add*
   those columns call `dbo.REPLACEASCII`; the ALTER fails, the column is never added,
   and every view/SP on it then fails.
4. `references invalid table` / `Invalid object name` — pure cascade from a parent
   table, function, or alter that failed for reasons 1–3.

Fix the function and partition orderings and the entire list collapses.

## Goal

Make the deployer order scripts by **what each script actually creates**, so a
dependency's object type always deploys before the object types that consume it —
regardless of how (or whether) the author numbers folders. No DB-script files are
renamed, moved, or edited; only the tool's in-memory ordering changes.

## Non-Goals

- View→view ordering. The FK topo-sort parses only `CREATE TABLE`/`REFERENCES`, so a
  view that selects from another view in the same rank is not ordered. A small number
  of real errors are view-on-view (e.g. `RPT_SALESQUOTATIONVIEW`→`salesinvoiceview`,
  `TAXRECONCILATIONSUMMARYVIEW`→`TAXRECONCILATIONPAYABLEANDRECEIVABLE`) and may need a
  re-run or a follow-up change. Called out, not silently "fixed".
- Schemabound functions that read tables. SQL Server defers table-name resolution for
  ordinary (non-schemabound) functions, so functions-before-tables is safe for them. A
  `WITH SCHEMABINDING` function that reads a table is a genuine table↔function cycle the
  author must break; the tool does not paper over it.
- Rollback orchestration, security fixes — unchanged from prior scope.

## Design

### Object-type classification (new, pure, testable)

`ScriptDependencyResolver.ClassifyKind(string sql) → SqlObjectKind`, decided from the
noise-stripped SQL (reusing the existing `StripNoise`) by keyword, checked in this
precedence so a multi-statement file classifies by its most significant DDL:

| `SqlObjectKind` | Detected by (case-insensitive) | Rank |
|---|---|---|
| `PartitionInfra` | `ADD FILEGROUP`, `ADD FILE`, `CREATE PARTITION FUNCTION`, `CREATE PARTITION SCHEME` | 0 |
| `Sequence` | `CREATE SEQUENCE` | 1 |
| `Function` | `CREATE` … `FUNCTION` (incl. `OR ALTER`) | 2 |
| `Table` | `CREATE TABLE` | 3 |
| `AlterTable` | `ALTER TABLE` (includes constraint adds) | 4 |
| `Index` | `CREATE` … `INDEX` | 5 |
| `View` | `CREATE` … `VIEW` (incl. `OR ALTER`) | 6 |
| `Procedure` | `CREATE` … `PROC` (incl. `OR ALTER`) | 7 |
| `Trigger` | `CREATE` … `TRIGGER` (incl. `OR ALTER`) | 8 |
| `Data` | `INSERT`/`UPDATE`/`MERGE` | 9 |
| `Unknown` | nothing matched | 10 |

Precedence note: check `CREATE TABLE` before `ALTER TABLE` so a create-plus-alter file
classifies as `Table`. `PartitionInfra`/`Sequence`/`Function` are checked before `Table`.

### Ordering keys (revised `Resolve`)

When `autoOrder = true`, the base order becomes:

1. **Type rank** (primary) — `(int)ClassifyKind(sql)`.
2. **Folder phase** (secondary) — the existing `ScriptNode.Phase` leading-digit number,
   so within a rank the author's `1. Create` still precedes `2. Alter`, and
   `4. Create Partition Function` precedes `5. Create Partition Scheme`.
3. **Name** (tertiary) — stable `Id` (relative path) order.

The FK topo-sort and edge construction, and cycle detection, are unchanged except that
groups and the `parent/child` same-group test key on **type rank** instead of phase
(so the topo-sort runs within the `Table` rank — exactly where `CREATE TABLE`/
`REFERENCES` edges live). `ClassifyKind` results are cached in a dictionary keyed by
node to avoid re-parsing.

Resulting canonical order: **partition infra → sequences → functions → tables → alters
→ indexes → views → procedures → triggers → data → unknown.**

### Escape hatch

When `autoOrder = false`, ordering stays exactly as today: `phase → name`, no type
rank, no topo-sort, no edges. Any database that genuinely depends on literal folder
order can still get it by turning the existing toggle off.

## Components

**Changed**
- `SqlDeployer.Core/Services/ScriptDependencyResolver.cs` — add `enum SqlObjectKind`,
  `ClassifyKind`, rework the `autoOrder` branch of `Resolve` to rank → phase → name and
  group/topo by rank.

**Tests**
- `SqlDeployer.Core.Tests/ScriptDependencyResolverTests.cs` — new cases (below). Existing
  cases remain valid: `Phase_order_beats_dependencies_and_name` holds because
  `CREATE TABLE` (rank 3) precedes `ALTER TABLE` (rank 4); the `autoOrder:false` case is
  unchanged; cycle/self-ref/independent cases all sit within the `Table` rank.

## Testing Strategy (TDD)

`ClassifyKind`:
- Each kind from a representative snippet, including `CREATE PARTITION SCHEME`,
  `CREATE FUNCTION`, `CREATE OR ALTER VIEW`, `ALTER TABLE … ADD`, `INSERT`.
- A create-plus-alter file → `Table` (precedence).
- Unrecognized text → `Unknown`.

`Resolve` (type ranking):
- Function script + table script that uses it, given to `Resolve` in reverse order →
  function emitted before table, independent of folder phase (e.g. function in phase 5,
  table in phase 1).
- Partition-scheme script (phase 1, nested) + partitioned table (phase 1, `1. Create`)
  → partition infra before table even though `"1. Create"` < `"1. Partitioning Script"`
  by name.
- View script + table it reads → table before view across ranks.
- Within the `Table` rank, FK parent-before-child still holds.
- `autoOrder:false` → falls back to phase+name (existing test stays green).

## Assumptions

- Functions are non-schemabound (deferred table resolution) — true for the failing UDFs.
- One primary DDL statement per file determines its kind; multi-kind files resolve by
  the precedence table.
