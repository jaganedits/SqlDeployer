using Microsoft.Data.SqlClient;

namespace SqlDeployer.Services;

// Pure builder mirroring the original Form1.GetConnectionString behavior:
// SQL auth when a login is supplied, otherwise Windows integrated auth.
public static class ConnectionStringFactory
{
    public static string Build(string server, string login, string password, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server.Trim(),
            InitialCatalog = database.Trim(),
            Encrypt = false
        };

        var trimmedLogin = login.Trim();
        if (!string.IsNullOrEmpty(trimmedLogin))
        {
            builder.UserID = trimmedLogin;
            builder.Password = password;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }
}
