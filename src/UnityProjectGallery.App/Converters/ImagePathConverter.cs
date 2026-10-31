using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace UnityProjectGallery.App.Converters;

/// <summary>把缩略图或封面文件路径转换为已加载且冻结的 WPF 图片。</summary>
public sealed class ImagePathConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var decodeWidth = 0;
        if (parameter is string widthText)
        {
            int.TryParse(widthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out decodeWidth);
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            if (decodeWidth > 0)
            {
                bitmap.DecodePixelWidth = decodeWidth;
            }

            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            // 损坏或缺少编码器的文件交由界面显示占位图。
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
