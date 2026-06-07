using Microsoft.UI.Xaml.Controls;
using SqlDeployer.ViewModels;

namespace SqlDeployerGui.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel Vm => App.SettingsVm;

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = Vm;
    }

    private void AccentSwatch_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string preset)
            Vm.SelectedAccentPreset = preset;
    }

    private void CustomPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        var c = args.NewColor;
        Vm.CustomAccentColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        Vm.SelectedAccentPreset = "Custom";
    }
}
