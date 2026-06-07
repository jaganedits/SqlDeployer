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
}
