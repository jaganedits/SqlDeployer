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
}
