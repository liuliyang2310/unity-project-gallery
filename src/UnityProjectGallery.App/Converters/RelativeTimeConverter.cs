using System.Globalization;
using System.Windows.Data;

namespace UnityProjectGallery.App.Converters;

/// <summary>把时间转换为适合卡片展示的相对时间文案。</summary>
public sealed class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset time)
        {
            return "从未";
        }

        var elapsed = DateTimeOffset.Now - time;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalMinutes < 1)
        {
            return "刚刚";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{(int)elapsed.TotalMinutes} 分钟前";
        }

        if (elapsed.TotalDays < 1)
        {
            return $"{(int)elapsed.TotalHours} 小时前";
        }

        if (elapsed.TotalDays < 2)
        {
            return "昨天";
        }

        if (elapsed.TotalDays < 7)
        {
            return $"{(int)elapsed.TotalDays} 天前";
        }

        return time.LocalDateTime.ToString("yyyy/MM/dd", CultureInfo.CurrentCulture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
