namespace SqlDeployer.Models;

public class AppSettings
{
    public ConnectionProfile? LastConnection { get; set; }

    // "Light", "Dark", or "Default" (follow system).
    public string Theme { get; set; } = "Default";
}
