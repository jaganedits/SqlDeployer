namespace SqlDeployer.Services;

// UI-agnostic dialog/picker abstraction so ViewModels stay testable.
// The WinUI app provides the concrete implementation (ContentDialog + FolderPicker).
public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);
    Task<string?> PickFolderAsync();
}
