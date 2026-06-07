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
}
