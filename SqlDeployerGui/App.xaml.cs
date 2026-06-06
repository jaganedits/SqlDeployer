using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
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

    public App() => InitializeComponent();

    private SplashWindow? _splash;
    private DispatcherQueueTimer? _splashTimer;   // field so it isn't GC'd before it ticks

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _splash = new SplashWindow();
        _splash.Activate();

        // Build the app only after the splash has painted its first frame. Doing the
        // (UI-thread) construction inline would block the splash and leave it black.
        _splash.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, BuildAppBehindSplash);
    }

    private void BuildAppBehindSplash()
    {
        Window = new MainWindow();

        var deployer = new SqlServerDeployer();
        var dialogs = new DialogService(Window);
        Settings = SettingsService.Default();

        Deploy = new DeployViewModel(new DeploymentRunner(deployer), deployer, dialogs, Settings);
        History = new HistoryViewModel(deployer);
        SettingsVm = new SettingsViewModel(Settings);

        SettingsVm.ThemeChanged += (_, theme) => ApplyTheme(theme);

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
}
