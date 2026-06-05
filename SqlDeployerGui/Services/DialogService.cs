using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SqlDeployer.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SqlDeployerGui.Services;

// Concrete IDialogService for WinUI. Folder picking in an UNPACKAGED app must
// be associated with the window HWND via InitializeWithWindow.
public class DialogService : IDialogService
{
    private readonly Window _window;

    public DialogService(Window window) => _window = window;

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = _window.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
