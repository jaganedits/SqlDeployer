using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using SqlDeployer.Models;

namespace SqlDeployerGui.Converters;

public class LogKindToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value switch
        {
            LogKind.Success => "LogSuccessBrush",
            LogKind.Error => "LogErrorBrush",
            _ => "LogInfoBrush",
        };
        return Application.Current.Resources.TryGetValue(key, out var brush)
            ? brush
            : Application.Current.Resources["LogInfoBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
