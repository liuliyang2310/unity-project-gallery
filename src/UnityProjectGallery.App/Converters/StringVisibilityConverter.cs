using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UnityProjectGallery.App.Converters;

/// <summary>空字符串转可见性，ConverterParameter 为 NotEmpty 时表示非空可见。</summary>
public sealed class StringVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasText = !string.IsNullOrEmpty(value as string);
        if (string.Equals(parameter as string, "NotEmpty", StringComparison.OrdinalIgnoreCase))
        {
            return hasText ? Visibility.Visible : Visibility.Collapsed;
        }

        return hasText ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
