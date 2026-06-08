using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using SqlDeployer;
using SqlDeployer.Services;
using SqlDeployer.ViewModels;
using SqlDeployerGui.Services;

namespace SqlDeployerGui;

public partial class App : Application
{
    public static MainWindow Window { get; private set; } = null!;

    public static SettingsService Settings { get; private set; } = null!;
    public static DeployViewModel Deploy { get; private set; } = null!;
    public static HistoryViewModel History { get; private set; } = null!;
    public static SettingsViewModel SettingsVm { get; private set; } = null!;
    public static ThemeService Theme { get; private set; } = null!;
    public static UpdateService Updates { get; private set; } = null!;

    public App() => InitializeComponent();

    private SplashWindow? _splash;
    private DispatcherQueueTimer? _splashTimer;   // field so it isn't GC'd before it ticks
    private DispatcherQueueTimer? _startTimer;
    private bool _appBuilt;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _splash = new SplashWindow();
        _splash.Activate();

        // Build after the splash has fully drawn (logo decoded + intro animation),
        // so the ~500ms UI-thread construction doesn't freeze the splash mid-fade
        // and delay the logo from appearing.
        _startTimer = _splash.DispatcherQueue.CreateTimer();
        _startTimer.Interval = TimeSpan.FromMilliseconds(700);
        _startTimer.IsRepeating = false;
        _startTimer.Tick += (timer, _) => { timer.Stop(); BuildAppBehindSplash(); };
        _startTimer.Start();
    }

    private void BuildAppBehindSplash()
    {
        if (_appBuilt) return;
        _appBuilt = true;
        _startTimer?.Stop();
        _startTimer = null;

        Settings = SettingsService.Default();

        // Apply the saved accent BEFORE constructing the window so controls bind to
        // our accent brush instances (which we later mutate for live switching).
        Theme = new ThemeService(Settings);
        Theme.ApplyFromSettings();

        Window = new MainWindow();
        Theme.ApplyBackdrop(Window);

        var deployer = new SqlServerDeployer();
        var dialogs = new DialogService(Window);

        // Enable Windows toasts for deploy outcomes; tear down on window close.
        AppNotificationManager.Default.Register();
        Window.Closed += (_, _) => AppNotificationManager.Default.Unregister();
        var notifier = new ToastNotifier(Window);

        Deploy = new DeployViewModel(new DeploymentRunner(deployer), deployer, dialogs, Settings, notifier);
        History = new HistoryViewModel(deployer);
        SettingsVm = new SettingsViewModel(Settings);
        Updates = new UpdateService();

        SettingsVm.ThemeChanged += (_, theme) => ApplyTheme(theme);
        SettingsVm.AccentChanged += (_, sel) =>
        {
            Theme.Apply(sel);
            // Accent brushes/colors are swapped in Application.Resources, but the
            // already-realized visual tree won't re-read them on its own — only a
            // RequestedTheme change forces ThemeResource re-resolution. At startup
            // that happens for free (ApplyTheme runs right after Apply); at runtime
            // we have to nudge it ourselves, otherwise the new accent only shows
            // after a restart.
            RefreshThemeResources();
        };

        // Keep the splash up a short beat after the app is ready, then hand off.
        _splashTimer = _splash!.DispatcherQueue.CreateTimer();
        _splashTimer.Interval = TimeSpan.FromSeconds(1.6);
        _splashTimer.IsRepeating = false;
        _splashTimer.Tick += (timer, _) =>
        {
            timer.Stop();
            _splashTimer = null;

            Window.Activate();
            ApplyTheme(SettingsVm.SelectedTheme);

            // Look for a newer GitHub release in the background; prompt once if one
            // is downloaded and ready. No-op for dev/xcopy runs (not installed).
            _ = CheckForUpdatesAsync();

            // Close the splash only after the main window has painted a frame,
            // otherwise there is a brief black gap between the two windows.
            var dq = Window.DispatcherQueue;
            dq.TryEnqueue(DispatcherQueuePriority.Low, () =>
                dq.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    _splash?.Close();
                    _splash = null;
                }));
        };
        _splashTimer.Start();
    }

    private static async Task CheckForUpdatesAsync()
    {
        var result = await Updates.CheckAndDownloadAsync();
        if (result.Status == UpdateStatus.UpdateReady)
            Window.ShowUpdateBanner(result.Version!);
    }

    public static void ApplyTheme(string theme)
    {
        if (Window.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    // Forces the live visual tree to re-resolve ThemeResource references (so a
    // runtime accent swap is picked up immediately). Flips RequestedTheme to a
    // different value and straight back in the same synchronous pass: each set
    // re-resolves theme resources, and because the final value equals the original
    // the intermediate value is never rendered — no visible flash.
    private static void RefreshThemeResources()
    {
        if (Window?.Content is not FrameworkElement root) return;

        var current = root.RequestedTheme;
        var nudge = root.ActualTheme == ElementTheme.Dark
            ? ElementTheme.Light
            : ElementTheme.Dark;

        root.RequestedTheme = nudge;
        root.RequestedTheme = current;
    }
}
