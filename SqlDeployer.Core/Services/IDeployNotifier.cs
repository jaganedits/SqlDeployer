namespace SqlDeployer.Services;

// The terminal outcome of a deployment run, used to pick the notification's
// wording/severity (e.g. an "urgent" toast and error sound for failures).
public enum DeployNotificationKind
{
    Finished,
    Failed,
    Stopped
}

// Raises an out-of-app notification (toast / taskbar flash / sound) when a
// deployment ends. Implementations decide whether to actually surface it —
// e.g. the GUI skips it while the window is focused, since the in-app banner
// already covers that case. Kept platform-free so the Core/ViewModel stays
// testable without WinUI.
public interface IDeployNotifier
{
    void Notify(DeployNotificationKind kind, string title, string message);
}
