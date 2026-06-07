namespace SqlDeployer.Models;

public class AppSettings
{
    // The most recently used connection (prefilled on startup).
    public ConnectionProfile? LastConnection { get; set; }

    // Saved connections keyed by server, so selecting a server refills its credentials.
    public List<ConnectionProfile> SavedConnections { get; set; } = new();

    // "Light", "Dark", or "Default" (follow system).
    public string Theme { get; set; } = "Default";

    // When true, deploys auto-order scripts by detected foreign-key dependencies.
    public bool AutoOrderByDependencies { get; set; } = true;
}
