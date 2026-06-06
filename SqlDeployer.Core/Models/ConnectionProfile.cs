namespace SqlDeployer.Models;

// Persisted connection info. The password is stored only as a DPAPI-encrypted
// blob in Secret (per Windows user) — never as plaintext on disk.
public class ConnectionProfile
{
    public string Server { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string ScriptPath { get; set; } = string.Empty;

    // DPAPI-encrypted password (base64). Empty when no password was saved.
    public string Secret { get; set; } = string.Empty;
}
