using System.Globalization;
using System.Windows.Data;

namespace UnityProjectGallery.App.Converters;

/// <summary>取出工程名称首字符，用作无封面时的视觉标记。</summary>
public sealed class ProjectInitialConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value as string;
        return string.IsNullOrWhiteSpace(name)
            ? "U"
            : name.Trim()[0].ToString().ToUpperInvariant();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
