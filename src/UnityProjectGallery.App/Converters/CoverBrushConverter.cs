using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UnityProjectGallery.App.Converters;

/// <summary>
/// 扫描阶段暂不处理真实封面图，根据工程名称生成稳定的渐变占位封面。
/// 后续接入封面素材后，此转换器仅作为缺省封面使用。
/// </summary>
public sealed class CoverBrushConverter : IValueConverter
{
    private static readonly (uint Start, uint End)[] Palettes =
    {
        (0xFF5A4FCF, 0xFFB23FA6),
        (0xFF1F6FEB, 0xFF2FA8C8),
        (0xFF6A4FE0, 0xFF3A6FF7),
        (0xFFD05563, 0xFFEE8A4B),
        (0xFF2E8F7F, 0xFF1F6F78),
        (0xFF8356E8, 0xFFD946EF),
        (0xFF3F7CAC, 0xFF5B4B8A),
        (0xFFA9783F, 0xFFD4653F)
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value as string ?? string.Empty;
        var hash = 0;
        foreach (var character in name)
        {
            hash = unchecked(hash * 31 + character);
        }

        var palette = Palettes[(hash & int.MaxValue) % Palettes.Length];
        var brush = new LinearGradientBrush(
            Color.FromArgb((byte)(palette.Start >> 24), (byte)(palette.Start >> 16), (byte)(palette.Start >> 8), (byte)palette.Start),
            Color.FromArgb((byte)(palette.End >> 24), (byte)(palette.End >> 16), (byte)(palette.End >> 8), (byte)palette.End),
            new System.Windows.Point(0, 0),
            new System.Windows.Point(1, 1));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
