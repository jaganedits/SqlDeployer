namespace SqlDeployer.Models;

// Persisted connection info. Intentionally has NO password field —
// passwords are never written to disk (security decision).
public class ConnectionProfile
{
    public string Server { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string ScriptPath { get; set; } = string.Empty;
}
