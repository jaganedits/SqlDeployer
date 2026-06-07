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
