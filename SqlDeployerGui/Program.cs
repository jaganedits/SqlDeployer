using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace SqlDeployerGui;

// Custom entry point (DISABLE_XAML_GENERATED_MAIN is set in the .csproj) so the
// Velopack hook runs before any WinUI bootstrapping. During install / update /
// uninstall, Velopack relaunches the exe with special arguments; VelopackApp.Run()
// handles those and exits the process, so it MUST be the very first thing we do.
public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
