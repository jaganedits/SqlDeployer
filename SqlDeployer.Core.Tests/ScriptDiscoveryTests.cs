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
