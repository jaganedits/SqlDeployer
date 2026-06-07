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
    public void PhaseOf_does_not_overflow_on_long_digit_runs()
    {
        // A 14-digit timestamp prefix exceeds int.MaxValue; it must not throw, and
        // such an oversized "phase" sorts last alongside unnumbered scripts.
        Assert.Equal(int.MaxValue, SqlServerDeployer.PhaseOf("20240101120000_seed.sql"));
        Assert.Equal(int.MaxValue, SqlServerDeployer.PhaseOf(Path.Combine("99999999999.Hotfix", "x.sql")));

        // A leading number that still fits in int is parsed normally (digits stop at '_').
        Assert.Equal(20240101, SqlServerDeployer.PhaseOf("20240101_001_add.sql"));
    }

    [Fact]
    public void UnambiguousLeaves_keeps_only_filenames_that_occur_once()
    {
        var nodes = new[]
        {
            new ScriptNode(Path.Combine("1.Table", "Users.sql"), 1, ""),
            new ScriptNode(Path.Combine("2.Alter", "Users.sql"), 2, ""),
            new ScriptNode(Path.Combine("1.Table", "Orders.sql"), 1, ""),
        };

        var leaves = SqlServerDeployer.UnambiguousLeaves(nodes);

        Assert.Contains("Orders.sql", leaves);
        Assert.DoesNotContain("Users.sql", leaves); // appears twice -> ambiguous
    }

    [Fact]
    public void IsAlreadyDeployed_matches_relative_path_identity()
    {
        var deployed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { Path.Combine("1.Table", "Users.sql") };
        var leaves = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Users.sql" };

        Assert.True(SqlServerDeployer.IsAlreadyDeployed(Path.Combine("1.Table", "Users.sql"), deployed, leaves));
        Assert.False(SqlServerDeployer.IsAlreadyDeployed(Path.Combine("2.Alter", "Users.sql"), deployed, leaves));
    }

    [Fact]
    public void IsAlreadyDeployed_matches_legacy_bare_filename_when_leaf_is_unambiguous()
    {
        // Legacy history stored the top-level filename; the script has since moved
        // into a phase folder. It must still be recognized as already deployed.
        var deployed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Users.sql" };
        var leaves = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Users.sql" };

        Assert.True(SqlServerDeployer.IsAlreadyDeployed(Path.Combine("1.Table", "Users.sql"), deployed, leaves));
    }

    [Fact]
    public void IsAlreadyDeployed_ignores_legacy_match_when_leaf_is_ambiguous()
    {
        // Two scripts share a filename, so a bare-filename legacy row can't be
        // attributed to one of them; re-running (returning false) is the safe choice.
        var deployed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Users.sql" };
        var leaves = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // "Users.sql" excluded

        Assert.False(SqlServerDeployer.IsAlreadyDeployed(Path.Combine("1.Table", "Users.sql"), deployed, leaves));
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
