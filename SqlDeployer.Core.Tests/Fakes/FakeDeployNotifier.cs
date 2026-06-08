using SqlDeployer.Services;

namespace SqlDeployer.Core.Tests.Fakes;

// Records notifications so tests can assert which deploy outcomes notify.
public class FakeDeployNotifier : IDeployNotifier
{
    public record Notification(DeployNotificationKind Kind, string Title, string Message);

    public List<Notification> Notifications { get; } = new();

    public void Notify(DeployNotificationKind kind, string title, string message)
        => Notifications.Add(new Notification(kind, title, message));
}
