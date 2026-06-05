using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using SqlDeployer.Models;
using Windows.UI;

namespace SqlDeployerGui.Converters;

public class LogKindToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Success = new(Color.FromArgb(255, 78, 201, 130));
    private static readonly SolidColorBrush Error = new(Color.FromArgb(255, 232, 106, 102));
    private static readonly SolidColorBrush Info = new(Colors.Gray);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is LogKind k
            ? k switch { LogKind.Success => Success, LogKind.Error => Error, _ => Info }
            : Info;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
