using SqlDeployer.Services;
using Xunit;

namespace SqlDeployer.Core.Tests;

public class ConnectionStringFactoryTests
{
    [Fact]
    public void Uses_sql_auth_when_login_provided()
    {
        var cs = ConnectionStringFactory.Build("myserver", "sa", "secret", "mydb");
        Assert.Contains("Data Source=myserver", cs);
        Assert.Contains("Initial Catalog=mydb", cs);
        Assert.Contains("User ID=sa", cs);
        Assert.Contains("Password=secret", cs);
        Assert.DoesNotContain("Integrated Security=True", cs);
    }

    [Fact]
    public void Uses_windows_auth_when_login_blank()
    {
        var cs = ConnectionStringFactory.Build("myserver", "   ", "ignored", "mydb");
        Assert.Contains("Integrated Security=True", cs);
        Assert.DoesNotContain("User ID=", cs);
    }

    [Fact]
    public void Trims_server_and_database_and_login()
    {
        var cs = ConnectionStringFactory.Build("  myserver ", " sa ", "secret", " mydb ");
        Assert.Contains("Data Source=myserver", cs);
        Assert.Contains("Initial Catalog=mydb", cs);
        Assert.Contains("User ID=sa", cs);
    }

    [Fact]
    public void Disables_encryption_to_match_existing_behavior()
    {
        var cs = ConnectionStringFactory.Build("s", "", "", "d");
        Assert.Contains("Encrypt=False", cs);
    }

    [Fact]
    public void BuildForServer_connects_to_master_with_sql_auth()
    {
        var cs = ConnectionStringFactory.BuildForServer("srv", "sa", "secret");
        Assert.Contains("Initial Catalog=master", cs);
        Assert.Contains("User ID=sa", cs);
        Assert.Contains("Password=secret", cs);
    }

    [Fact]
    public void BuildForServer_uses_windows_auth_when_login_blank()
    {
        var cs = ConnectionStringFactory.BuildForServer("srv", "  ", "");
        Assert.Contains("Initial Catalog=master", cs);
        Assert.Contains("Integrated Security=True", cs);
        Assert.DoesNotContain("User ID=", cs);
    }
}
