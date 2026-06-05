using SqlDeployer;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class SmokeTest
{
    [Fact]
    public void DeploymentScript_record_holds_its_values()
    {
        var script = new DeploymentScript("001_init.sql", "001", IsRollback: false);
        Assert.Equal("001", script.Version);
        Assert.False(script.IsRollback);
    }
}
