using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using SqlDeployer.Services;

namespace SqlDeployer;

public record DeploymentScript(string FileName, string Version, bool IsRollback);
public record DeploymentHistory(string ScriptName, string Version, DateTime DeployedAt, bool Success, string? ErrorMessage = null);

// One row per *.sql file found in the scripts folder, with why it will or won't run.
public record ScriptStatus(string FileName, string Version, bool IsRollback, bool AlreadyDeployed);

public class SqlServerDeployer : ISqlDeployer
{
    private readonly string _configPath;
    
    private readonly DeploymentConfig _config;

    public SqlServerDeployer()
    {
        _configPath = string.Empty;
        _config = new DeploymentConfig();
    }

    public SqlServerDeployer(string configPath)
    {
        _configPath = configPath;
        _config = LoadConfiguration(configPath);
    }

    // Leading integer of the first path segment ("1.Table" -> 1). Unnumbered
    // folders/files sort last (int.MaxValue).
    public static int PhaseOf(string relativePath)
    {
        var first = relativePath.Split(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        var digits = new string(first.TakeWhile(char.IsDigit).ToArray());
        return digits.Length > 0 ? int.Parse(digits) : int.MaxValue;
    }

    // Recursively find every *.sql under rootPath and build a ScriptNode for each:
    // Id = path relative to root (stable identity), Phase from the first folder,
    // NameKey = relative path (tertiary sort), Sql = file content.
    public static IReadOnlyList<ScriptNode> DiscoverScripts(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Scripts directory not found: {rootPath}");

        var nodes = new List<ScriptNode>();
        foreach (var file in Directory.GetFiles(rootPath, "*.sql", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(rootPath, file);
            nodes.Add(new ScriptNode(relative, PhaseOf(relative), relative, File.ReadAllText(file)));
        }
        return nodes;
    }

    public string? GetConnectionString(string environment)
    {
        var env = _config.Environments.FirstOrDefault(e => 
            e.Name.Equals(environment, StringComparison.OrdinalIgnoreCase));
        if (env == null) return null;

        if (!string.IsNullOrEmpty(env.ConnectionString))
        {
            return env.ConnectionString;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = env.Server,
            InitialCatalog = env.Database,
            Encrypt = false
        };

        if (!string.IsNullOrEmpty(env.Username))
        {
            builder.UserID = env.Username;
            builder.Password = env.Password;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }

    public async Task<List<DeploymentScript>> GetPendingScripts(string scriptsPath, string environment, string connectionString, CancellationToken cancellationToken = default, bool includeDeployed = false)
    {
        // When includeDeployed is true the deploy history is ignored, so every
        // (non-rollback) script is re-run regardless of whether it ran before.
        var deployedVersions = includeDeployed
            ? new HashSet<string>()
            : new HashSet<string>(await GetDeployedScripts(connectionString, cancellationToken));

        var scripts = new List<DeploymentScript>();

        if (!Directory.Exists(scriptsPath))
        {
            throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsPath}");
        }

        // Get all SQL files sorted by version (numeric-aware so 2 sorts before 10)
        var sqlFiles = Directory.GetFiles(scriptsPath, "*.sql")
            .OrderBy(f => VersionSortKey(ExtractVersion(Path.GetFileNameWithoutExtension(f))), StringComparer.Ordinal)
            .ToList();

        foreach (var file in sqlFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var version = ExtractVersion(fileName);
            var isRollback = fileName.EndsWith("_rollback", StringComparison.OrdinalIgnoreCase);

            // Include scripts not yet deployed (or all of them when re-running), never rollbacks.
            if (!deployedVersions.Contains(version) && !isRollback)
            {
                scripts.Add(new DeploymentScript(file, version, isRollback));
            }
        }

        return scripts;
    }

    // Diagnostic scan: every *.sql file in the folder with its computed version,
    // whether it's a rollback, and whether that version was already deployed. Lets
    // the UI explain why a deploy found "no pending scripts".
    public async Task<List<ScriptStatus>> GetScriptStatuses(string scriptsPath, string connectionString, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(scriptsPath))
            throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsPath}");

        var deployedVersions = new HashSet<string>(await GetDeployedScripts(connectionString, cancellationToken));

        return Directory.GetFiles(scriptsPath, "*.sql")
            .OrderBy(f => VersionSortKey(ExtractVersion(Path.GetFileNameWithoutExtension(f))), StringComparer.Ordinal)
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var version = ExtractVersion(name);
                var isRollback = name.EndsWith("_rollback", StringComparison.OrdinalIgnoreCase);
                return new ScriptStatus(Path.GetFileName(f), version, isRollback, deployedVersions.Contains(version));
            })
            .ToList();
    }

    public async Task ExecuteScript(string connectionString, string scriptPath, string version, string environment, CancellationToken cancellationToken = default)
    {
        var scriptContent = await File.ReadAllTextAsync(scriptPath, cancellationToken);

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            // Create deployment tracking table if it doesn't exist
            await CreateDeploymentTrackingTable(connection, cancellationToken);

            // Split script into batches by 'GO' keyword on its own line
            var batches = System.Text.RegularExpressions.Regex.Split(
                scriptContent,
                @"^\s*GO\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline
            );

            bool scriptSuccess = false;
            string? errorMessage = null;

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var batch in batches)
                    {
                        var trimmedBatch = batch.Trim();
                        if (string.IsNullOrWhiteSpace(trimmedBatch)) continue;

                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = trimmedBatch;
                            command.CommandTimeout = 300; // 5 minutes timeout
                            await command.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }

                    transaction.Commit();
                    scriptSuccess = true;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is not a deployment failure — roll back and let the caller handle it
                    try { transaction.Rollback(); } catch { /* connection may already be torn down */ }
                    throw;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception rollbackEx)
                    {
                        errorMessage += $" (Rollback failed: {rollbackEx.Message})";
                    }
                }
            }

            // Record the deployment results in the audit table
            await LogDeployment(connection, Path.GetFileName(scriptPath), version, scriptSuccess, errorMessage, environment);

            if (!scriptSuccess)
            {
                throw new Exception(errorMessage);
            }
        }
    }

    private async Task<List<string>> GetDeployedScripts(string connectionString, CancellationToken cancellationToken = default)
    {
        var deployedVersions = new List<string>();

        try
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    IF OBJECT_ID('dbo.DeploymentHistory', 'U') IS NOT NULL
                    BEGIN
                        SELECT DISTINCT Version FROM dbo.DeploymentHistory WHERE Success = 1
                    END
                ";

                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        deployedVersions.Add(reader.GetString(0));
                    }
                }
            }
        }
        catch (SqlException)
        {
            // Table doesn't exist yet, which is fine
        }

        return deployedVersions;
    }

    // Lists user databases on the server (excludes the 4 system databases
    // master/tempdb/model/msdb via database_id > 4). Errors propagate to the caller.
    public async Task<List<string>> GetDatabases(string connectionString)
    {
        var databases = new List<string>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            databases.Add(reader.GetString(0));
        }

        return databases;
    }

    public async Task<List<DeploymentHistory>> GetDeploymentHistory(string connectionString)
    {
        var history = new List<DeploymentHistory>();

        try
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    IF OBJECT_ID('dbo.DeploymentHistory', 'U') IS NOT NULL
                    BEGIN
                        SELECT ScriptName, Version, DeployedAt, Success, ErrorMessage
                        FROM dbo.DeploymentHistory
                        ORDER BY DeployedAt DESC
                    END
                ";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        history.Add(new DeploymentHistory(
                            reader.GetString(0),
                            reader.GetString(1),
                            reader.GetDateTime(2),
                            reader.GetBoolean(3),
                            reader.IsDBNull(4) ? null : reader.GetString(4)
                        ));
                    }
                }
            }
        }
        catch (SqlException)
        {
            // Table doesn't exist
        }

        return history;
    }

    // Deletes all deployment history rows. After this, every script is treated as
    // pending again on the next deploy.
    public async Task ClearHistory(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            IF OBJECT_ID('dbo.DeploymentHistory', 'U') IS NOT NULL
                DELETE FROM dbo.DeploymentHistory";
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateDeploymentTrackingTable(SqlConnection connection, CancellationToken cancellationToken = default)
    {
        using var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            IF OBJECT_ID('dbo.DeploymentHistory', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DeploymentHistory (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ScriptName NVARCHAR(255) NOT NULL,
                    Version NVARCHAR(50) NOT NULL,
                    DeployedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    Environment NVARCHAR(50) NOT NULL,
                    Success BIT NOT NULL,
                    ErrorMessage NVARCHAR(MAX),
                    DeployedBy NVARCHAR(255)
                )
                
                CREATE INDEX IX_Version ON dbo.DeploymentHistory(Version)
                CREATE INDEX IX_Environment ON dbo.DeploymentHistory(Environment)
            END
        ";

        await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task LogDeployment(SqlConnection connection, string scriptName, string version, bool success, string? errorMessage, string environment)
    {
        var logCommand = connection.CreateCommand();
        logCommand.CommandText = @"
            INSERT INTO dbo.DeploymentHistory (ScriptName, Version, Environment, Success, ErrorMessage, DeployedBy)
            VALUES (@scriptName, @version, @environment, @success, @errorMessage, @deployedBy)
        ";

        logCommand.Parameters.AddWithValue("@scriptName", scriptName);
        logCommand.Parameters.AddWithValue("@version", version);
        logCommand.Parameters.AddWithValue("@environment", environment);
        logCommand.Parameters.AddWithValue("@success", success);
        logCommand.Parameters.AddWithValue("@errorMessage", (object?)errorMessage ?? DBNull.Value);
        logCommand.Parameters.AddWithValue("@deployedBy", Environment.UserName);

        await logCommand.ExecuteNonQueryAsync();
    }

    private DeploymentConfig LoadConfiguration(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        var json = File.ReadAllText(configPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<DeploymentConfig>(json, options)
            ?? throw new InvalidOperationException("Invalid configuration");

        return config;
    }

    private string ExtractVersion(string fileName)
    {
        // Version is the run of leading numeric, underscore-separated segments.
        //   001_script_name        -> "001"
        //   20240101_001_add_table -> "20240101_001"
        var parts = fileName.Split('_');
        var versionParts = parts.TakeWhile(p => p.Length > 0 && p.All(char.IsDigit)).ToList();

        // Fall back to the first segment if nothing numeric leads (preserves old behaviour).
        return versionParts.Count > 0 ? string.Join('_', versionParts) : parts[0];
    }

    // Builds an ordinally-comparable key where each numeric segment is left-padded,
    // so "2" sorts before "10" and "20240101_2" before "20240101_10".
    private static string VersionSortKey(string version)
    {
        return string.Join('_', version
            .Split('_')
            .Select(seg => seg.All(char.IsDigit) ? seg.PadLeft(19, '0') : seg));
    }
}

public class DeploymentConfig
{
    [JsonPropertyName("environments")]
    public List<EnvironmentConfig> Environments { get; set; } = new();
}

public class EnvironmentConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("connectionString")]
    public string ConnectionString { get; set; } = string.Empty;

    [JsonPropertyName("server")]
    public string? Server { get; set; }

    [JsonPropertyName("database")]
    public string? Database { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}
