using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using SqlDeployer.Models;

namespace SqlDeployerGui.Converters;

// Maps a LogKind to an InfoBar severity so the inline result banner shows
// green (success), red (error), or blue (informational).
public class LogKindToSeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is LogKind k
            ? k switch
            {
                LogKind.Success => InfoBarSeverity.Success,
                LogKind.Error => InfoBarSeverity.Error,
                _ => InfoBarSeverity.Informational
            }
            : InfoBarSeverity.Informational;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
