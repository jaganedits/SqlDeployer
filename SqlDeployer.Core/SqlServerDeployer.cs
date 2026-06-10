using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using SqlDeployer.Services;

namespace SqlDeployer;

// FileName = absolute path (read/execute). Version = relative-path identity:
// the history dedup key and the phase-qualified display name.
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
    // folders/files — and numeric prefixes too large to be a real phase — sort
    // last (int.MaxValue). Parsing via long avoids overflowing on long digit
    // runs (e.g. timestamp-style names like "20240101120000_seed.sql").
    public static int PhaseOf(string relativePath)
    {
        var first = relativePath.Split(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        var digits = new string(first.TakeWhile(char.IsDigit).ToArray());
        return long.TryParse(digits, out var phase) && phase <= int.MaxValue
            ? (int)phase
            : int.MaxValue;
    }

    // Recursively find every *.sql under rootPath and build a ScriptNode for each:
    // Id = path relative to root (stable identity, also the name sort key),
    // Phase from the first folder, Sql = file content.
    public static IReadOnlyList<ScriptNode> DiscoverScripts(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Scripts directory not found: {rootPath}");

        var nodes = new List<ScriptNode>();
        foreach (var file in Directory.GetFiles(rootPath, "*.sql", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(rootPath, file);
            nodes.Add(new ScriptNode(relative, PhaseOf(relative), File.ReadAllText(file)));
        }
        return nodes;
    }

    // A script whose file name (sans extension) ends with "_rollback" is a rollback
    // script and is never auto-deployed.
    public static bool IsRollbackScript(string id) =>
        Path.GetFileNameWithoutExtension(id).EndsWith("_rollback", StringComparison.OrdinalIgnoreCase);

    // Leaf filenames that occur exactly once across the discovered scripts. Used to
    // safely match a legacy history row (see IsAlreadyDeployed) without confusing two
    // like-named scripts that live in different folders.
    public static HashSet<string> UnambiguousLeaves(IEnumerable<ScriptNode> nodes) =>
        nodes.GroupBy(n => Path.GetFileName(n.Id), StringComparer.OrdinalIgnoreCase)
             .Where(g => g.Count() == 1)
             .Select(g => g.Key)
             .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // True when a discovered script counts as already deployed. The current identity
    // is the relative path; we also match a legacy bare-filename record (history
    // written before identities became relative paths stored only the leaf name) — but
    // only when that leaf is unambiguous, so relocating a top-level script into a phase
    // folder doesn't re-run it, while two like-named scripts stay independent.
    public static bool IsAlreadyDeployed(
        string id, ISet<string> deployed, ISet<string> unambiguousLeaves)
    {
        if (deployed.Contains(id)) return true;
        var leaf = Path.GetFileName(id);
        return unambiguousLeaves.Contains(leaf) && deployed.Contains(leaf);
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

    public async Task<List<DeploymentScript>> GetPendingScripts(
        string scriptsPath, string environment, string connectionString,
        CancellationToken cancellationToken = default,
        bool includeDeployed = false,
        bool autoOrder = true)
    {
        // Identity is the relative path; dedup against scripts already applied OK.
        var deployed = includeDeployed
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                await GetDeployedScripts(connectionString, cancellationToken),
                StringComparer.OrdinalIgnoreCase);

        var nodes = DiscoverScripts(scriptsPath); // throws DirectoryNotFoundException if missing
        var plan = ScriptDependencyResolver.Resolve(nodes, autoOrder);

        if (plan.Cycle.Count > 0)
            throw new InvalidOperationException(
                "Foreign-key cycle detected among: " + string.Join(", ", plan.Cycle) +
                ". Break the cycle (e.g. move one FK into an ALTER script).");

        var leaves = UnambiguousLeaves(plan.Order);
        var pending = new List<DeploymentScript>();
        foreach (var n in plan.Order)
        {
            if (IsRollbackScript(n.Id)) continue;
            if (IsAlreadyDeployed(n.Id, deployed, leaves)) continue;

            var fullPath = Path.Combine(scriptsPath, n.Id);
            pending.Add(new DeploymentScript(fullPath, n.Id, IsRollback: false));
        }
        return pending;
    }

    // Diagnostic scan: every *.sql file under the folder (recursively), in deploy
    // order, with whether it's a rollback and whether its relative-path identity was
    // already deployed. Lets the UI explain why a deploy found "no pending scripts".
    public async Task<List<ScriptStatus>> GetScriptStatuses(string scriptsPath, string connectionString, CancellationToken cancellationToken = default)
    {
        var deployed = new HashSet<string>(
            await GetDeployedScripts(connectionString, cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var nodes = DiscoverScripts(scriptsPath); // recursive; throws if folder missing
        var plan = ScriptDependencyResolver.Resolve(nodes);

        // FileName and Version both carry the relative-path identity so the
        // diagnostic matches the deploy's run order and dedup key.
        var leaves = UnambiguousLeaves(plan.Order);
        return plan.Order
            .Select(n => new ScriptStatus(
                n.Id, n.Id, IsRollbackScript(n.Id), IsAlreadyDeployed(n.Id, deployed, leaves)))
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

            // version == relative-path identity (from DeploymentScript.Version);
            // store it as ScriptName (NVARCHAR(255)); keep the leaf filename in Version.
            await LogDeployment(connection, version, Path.GetFileName(scriptPath),
                scriptSuccess, errorMessage, environment);

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
                        SELECT DISTINCT ScriptName FROM dbo.DeploymentHistory WHERE Success = 1
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
                    ScriptName NVARCHAR(500) NOT NULL,
                    Version NVARCHAR(255) NOT NULL,
                    DeployedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    Environment NVARCHAR(50) NOT NULL,
                    Success BIT NOT NULL,
                    ErrorMessage NVARCHAR(MAX),
                    DeployedBy NVARCHAR(255)
                )

                CREATE INDEX IX_ScriptName ON dbo.DeploymentHistory(ScriptName)
                CREATE INDEX IX_Environment ON dbo.DeploymentHistory(Environment)
            END
            ELSE
            BEGIN
                -- Widen identity/display columns on databases created before scripts
                -- were identified by relative path: ScriptName now holds the relative
                -- path and Version the leaf filename, both of which can exceed the
                -- original NVARCHAR(50)/(255). max_length is in bytes (NVARCHAR(n)=2n).
                IF EXISTS (SELECT 1 FROM sys.columns
                           WHERE object_id = OBJECT_ID('dbo.DeploymentHistory')
                             AND name = 'Version' AND max_length <> -1 AND max_length < 510)
                    ALTER TABLE dbo.DeploymentHistory ALTER COLUMN Version NVARCHAR(255) NOT NULL

                IF EXISTS (SELECT 1 FROM sys.columns
                           WHERE object_id = OBJECT_ID('dbo.DeploymentHistory')
                             AND name = 'ScriptName' AND max_length <> -1 AND max_length < 1000)
                    ALTER TABLE dbo.DeploymentHistory ALTER COLUMN ScriptName NVARCHAR(500) NOT NULL
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
