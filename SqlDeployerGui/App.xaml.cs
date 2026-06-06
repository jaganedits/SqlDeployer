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
        // Show the splash first; build the app behind it, then hand off.
        _splash = new SplashWindow();
        _splash.Activate();

        Window = new MainWindow();

        var deployer = new SqlServerDeployer();
        var dialogs = new DialogService(Window);
        Settings = SettingsService.Default();

        Deploy = new DeployViewModel(new DeploymentRunner(deployer), deployer, dialogs, Settings);
        History = new HistoryViewModel(deployer);
        SettingsVm = new SettingsViewModel(Settings);

        SettingsVm.ThemeChanged += (_, theme) => ApplyTheme(theme);

        _splashTimer = _splash.DispatcherQueue.CreateTimer();
        _splashTimer.Interval = TimeSpan.FromSeconds(2.6);
        _splashTimer.IsRepeating = false;
        _splashTimer.Tick += (_, _) =>
        {
            Window.Activate();
            ApplyTheme(SettingsVm.SelectedTheme);
            _splash?.Close();
            _splash = null;
            _splashTimer = null;
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
