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
