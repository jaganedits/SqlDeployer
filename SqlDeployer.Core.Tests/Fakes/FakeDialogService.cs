using SqlDeployer.Services;

namespace SqlDeployer.Core.Tests.Fakes;

public class FakeDialogService : IDialogService
{
    public List<(string Title, string Message)> Messages { get; } = new();
    public string? FolderToReturn { get; set; }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<string?> PickFolderAsync() => Task.FromResult(FolderToReturn);
}
