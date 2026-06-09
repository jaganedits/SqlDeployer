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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Throws_for_empty_table_name(string raw)
        => Assert.Throws<System.ArgumentException>(() => ScriptDependencyResolver.NormalizeTableName(raw));

    [Fact]
    public void Three_part_name_uses_last_two_segments()
        => Assert.Equal("dbo.orders", ScriptDependencyResolver.NormalizeTableName("catalog.dbo.Orders"));

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

    [Fact]
    public void Ignores_block_comment_syntax_inside_string_literal()
    {
        var sql = "SELECT '/* not a comment */ text' FROM t -- REFERENCES ignored";
        Assert.Empty(ScriptDependencyResolver.ReferencedTables(sql));
    }

    // ---- Task 3: Resolve tests ----

    private static ScriptNode Node(string id, int phase, string sql) => new(id, phase, sql);

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
        Assert.Empty(plan.Cycle);
    }

    [Fact]
    public void Parent_file_creating_two_referenced_tables_is_not_a_false_cycle()
    {
        var parent = Node("AB.sql", 1,
            "CREATE TABLE A ( id INT PRIMARY KEY ); CREATE TABLE B ( id INT PRIMARY KEY );");
        var child = Node("C.sql", 1,
            "CREATE TABLE C ( a INT, b INT, " +
            "CONSTRAINT fa FOREIGN KEY (a) REFERENCES A(id), " +
            "CONSTRAINT fb FOREIGN KEY (b) REFERENCES B(id) );");

        var plan = ScriptDependencyResolver.Resolve(new[] { child, parent });
        var order = plan.Order.Select(n => n.Id).ToList();

        Assert.Empty(plan.Cycle);
        Assert.True(order.IndexOf("AB.sql") < order.IndexOf("C.sql"));
    }

    // ---- Object-type classification ----

    [Theory]
    [InlineData("CREATE PARTITION SCHEME PartitionBymonth AS PARTITION PF ALL TO ([PRIMARY]);", SqlObjectKind.PartitionInfra)]
    [InlineData("CREATE PARTITION FUNCTION PF (datetime) AS RANGE RIGHT FOR VALUES ('2026-01-01');", SqlObjectKind.PartitionInfra)]
    [InlineData("ALTER DATABASE [db] ADD FILEGROUP FG_2026;", SqlObjectKind.PartitionInfra)]
    [InlineData("CREATE SEQUENCE dbo.Seq AS INT START WITH 1;", SqlObjectKind.Sequence)]
    [InlineData("CREATE FUNCTION dbo.FN_GETMETASUBDESC(@id INT) RETURNS INT AS BEGIN RETURN 1 END", SqlObjectKind.Function)]
    [InlineData("CREATE OR ALTER FUNCTION dbo.F() RETURNS INT AS BEGIN RETURN 1 END", SqlObjectKind.Function)]
    [InlineData("CREATE TABLE dbo.X ( id INT );", SqlObjectKind.Table)]
    [InlineData("ALTER TABLE dbo.X ADD col INT;", SqlObjectKind.AlterTable)]
    [InlineData("CREATE NONCLUSTERED INDEX IX ON dbo.X(id);", SqlObjectKind.Index)]
    [InlineData("CREATE VIEW dbo.V AS SELECT 1 AS x;", SqlObjectKind.View)]
    [InlineData("CREATE OR ALTER VIEW dbo.V AS SELECT 1 AS x;", SqlObjectKind.View)]
    [InlineData("CREATE PROCEDURE dbo.P AS SELECT 1;", SqlObjectKind.Procedure)]
    [InlineData("CREATE PROC dbo.P AS SELECT 1;", SqlObjectKind.Procedure)]
    [InlineData("CREATE TRIGGER trg ON dbo.X AFTER INSERT AS SELECT 1;", SqlObjectKind.Trigger)]
    [InlineData("INSERT INTO dbo.X (id) VALUES (1);", SqlObjectKind.Data)]
    [InlineData("UPDATE dbo.X SET id = 2;", SqlObjectKind.Data)]
    [InlineData("SELECT * FROM dbo.X;", SqlObjectKind.Unknown)]
    public void Classifies_object_kind(string sql, SqlObjectKind expected)
        => Assert.Equal(expected, ScriptDependencyResolver.ClassifyKind(sql));

    [Fact]
    public void Create_plus_alter_classifies_as_table()
        => Assert.Equal(SqlObjectKind.Table,
            ScriptDependencyResolver.ClassifyKind(
                "CREATE TABLE dbo.X (id INT); ALTER TABLE dbo.X ADD c INT;"));

    // ---- Object-type ranking in Resolve ----

    [Fact]
    public void Function_is_ordered_before_table_that_uses_it_regardless_of_phase()
    {
        var func = Node("5. Function/F_ACCOUNTINGYEAR.sql", 5,
            "CREATE FUNCTION dbo.F_ACCOUNTINGYEAR() RETURNS INT AS BEGIN RETURN 1 END");
        var table = Node("1. Tables/1. Create/JOURNAL.sql", 1,
            "CREATE TABLE dbo.JOURNAL ( id INT, yr AS (dbo.F_ACCOUNTINGYEAR()) );");

        var plan = ScriptDependencyResolver.Resolve(new[] { table, func });
        var order = plan.Order.Select(n => n.Id).ToList();

        Assert.True(order.IndexOf("5. Function/F_ACCOUNTINGYEAR.sql")
                  < order.IndexOf("1. Tables/1. Create/JOURNAL.sql"));
    }

    [Fact]
    public void Partition_infra_is_ordered_before_partitioned_table_in_same_phase()
    {
        // Both phase 1; by name "1. Create" < "1. Partitioning Script", so only type
        // ranking puts the scheme first.
        var scheme = Node("1. Tables/1. Partitioning Script/5. Create Partition Scheme.sql", 1,
            "CREATE PARTITION SCHEME PartitionBymonth AS PARTITION PF ALL TO ([PRIMARY]);");
        var table = Node("1. Tables/1. Create/SALESINVOICE.sql", 1,
            "CREATE TABLE dbo.SALESINVOICE ( id INT ) ON PartitionBymonth(id);");

        var plan = ScriptDependencyResolver.Resolve(new[] { table, scheme });
        var order = plan.Order.Select(n => n.Id).ToList();

        Assert.True(order.IndexOf("1. Tables/1. Partitioning Script/5. Create Partition Scheme.sql")
                  < order.IndexOf("1. Tables/1. Create/SALESINVOICE.sql"));
    }

    [Fact]
    public void Table_is_ordered_before_view_then_procedure()
    {
        var view = Node("2. Views/V.sql", 2, "CREATE VIEW dbo.V AS SELECT id FROM dbo.X;");
        var proc = Node("3. SP/P.sql", 3, "CREATE PROCEDURE dbo.P AS SELECT id FROM dbo.X;");
        var table = Node("1. Tables/X.sql", 1, "CREATE TABLE dbo.X ( id INT );");

        var plan = ScriptDependencyResolver.Resolve(new[] { proc, view, table });
        var order = plan.Order.Select(n => n.Id).ToList();

        Assert.True(order.IndexOf("1. Tables/X.sql") < order.IndexOf("2. Views/V.sql"));
        Assert.True(order.IndexOf("2. Views/V.sql") < order.IndexOf("3. SP/P.sql"));
    }

    [Fact]
    public void Same_kind_orders_by_folder_phase_then_name()
    {
        var nested = Node("1. Tables/1. Partitioning Script/4. Create Partition Function.sql", 1,
            "CREATE PARTITION FUNCTION PF (int) AS RANGE RIGHT FOR VALUES (1);");
        var topLevel = Node("11.Partition_Script/Partition_Script_2026.sql", 11,
            "CREATE PARTITION SCHEME PS AS PARTITION PF ALL TO ([PRIMARY]);");

        var plan = ScriptDependencyResolver.Resolve(new[] { topLevel, nested });

        Assert.Equal(
            new[]
            {
                "1. Tables/1. Partitioning Script/4. Create Partition Function.sql",
                "11.Partition_Script/Partition_Script_2026.sql",
            },
            plan.Order.Select(n => n.Id));
    }

    [Fact]
    public void FK_topo_sort_still_applies_within_the_table_rank()
    {
        var parent = Node("Planmaster.sql", 5, "CREATE TABLE Planmaster ( planid INT PRIMARY KEY );");
        var child = Node("PlanFeatureDetail.sql", 1,
            "CREATE TABLE planfeaturedetail ( planid INT, CONSTRAINT fk FOREIGN KEY (planid) REFERENCES planmaster(planid) );");

        var plan = ScriptDependencyResolver.Resolve(new[] { child, parent });
        var order = plan.Order.Select(n => n.Id).ToList();

        // Both are Table rank; FK edge wins over the differing folder phase.
        Assert.True(order.IndexOf("Planmaster.sql") < order.IndexOf("PlanFeatureDetail.sql"));
        Assert.Empty(plan.Cycle);
    }
}
