using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using SqlDeployer.Models;
using SqlDeployer.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace SqlDeployerGui.Views;

public sealed partial class DeployPage : Page
{
    // App.Deploy is assigned during startup; this page can be constructed before
    // that happens, so Vm-dependent wiring is deferred to Loaded (where it is set).
    public DeployViewModel Vm => App.Deploy;

    public DeployPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DataContext = Vm;

        // Keep the terminal scrolled to the newest line as logs stream in.
        Vm.SuccessLog.CollectionChanged += OnSuccessLogChanged;
        Vm.ErrorLog.CollectionChanged += OnErrorLogChanged;

        ((Storyboard)Resources["BlinkCaret"]).Begin();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Vm.SuccessLog.CollectionChanged -= OnSuccessLogChanged;
        Vm.ErrorLog.CollectionChanged -= OnErrorLogChanged;
    }

    private void OnSuccessLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScrollToEnd(SuccessList, Vm.SuccessLog, e);

    private void OnErrorLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScrollToEnd(ErrorList, Vm.ErrorLog, e);

    private void ScrollToEnd(ListView list, IReadOnlyList<LogEntry> items, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && items.Count > 0)
            DispatcherQueue.TryEnqueue(() => list.ScrollIntoView(items[^1]));
    }

    // --- Server autocomplete: pick a saved server to refill its credentials ---

    private void ServerBox_GotFocus(object sender, RoutedEventArgs e)
        => ServerBox.ItemsSource = FilterServers(ServerBox.Text);

    private void ServerBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            sender.ItemsSource = FilterServers(sender.Text);
    }

    private void ServerBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string server)
        {
            sender.Text = server;
            Vm.ApplyServerProfile(server);
        }
    }

    private void ServerBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => Vm.ApplyServerProfile((args.ChosenSuggestion as string) ?? sender.Text);

    private List<string> FilterServers(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Vm.SavedServers.ToList();

        return Vm.SavedServers
            .Where(s => s.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // --- Database autocomplete (AutoSuggestBox over the loaded database list) ---

    private async void DatabaseBox_GotFocus(object sender, RoutedEventArgs e)
    {
        // Lazily pull the database list from the server the first time the field is used.
        if (Vm.Databases.Count == 0 && !string.IsNullOrWhiteSpace(Vm.Server) && !Vm.IsBusy)
            await Vm.LoadDatabasesCommand.ExecuteAsync(null);

        DatabaseBox.ItemsSource = Filter(DatabaseBox.Text);
    }

    private void DatabaseBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            sender.ItemsSource = Filter(sender.Text);
    }

    private void DatabaseBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string s) sender.Text = s;
    }

    private List<string> Filter(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Vm.Databases.ToList();

        return Vm.Databases
            .Where(d => d.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // --- Copy the active output tab to the clipboard ---

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        var entries = OutputPivot.SelectedIndex switch
        {
            1 => Vm.ErrorLog,
            2 => (IReadOnlyList<LogEntry>)Vm.PendingLog,
            _ => Vm.SuccessLog
        };
        var text = string.Join(Environment.NewLine, entries.Select(x => x.Message));

        var data = new DataPackage();
        data.SetText(text);
        Clipboard.SetContent(data);
    }
}
