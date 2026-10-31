using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace UnityProjectGallery.App.Behaviors;

/// <summary>
/// 将 TextBlock 中的 http/https 网址渲染为可点击链接，点击后使用系统默认浏览器打开。
/// </summary>
public static class LinkedTextBehavior
{
    private static readonly Regex UrlPattern = new(
        @"(?:https?://|www\.)[^\s<>""'，。；：！？、）】》」]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(LinkedTextBehavior),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public static void SetText(DependencyObject element, string? value) =>
        element.SetValue(TextProperty, value);

    public static string GetText(DependencyObject element) =>
        (string)element.GetValue(TextProperty);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
        {
            return;
        }

        textBlock.Inlines.Clear();
        var text = e.NewValue as string;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var linkBrush = textBlock.TryFindResource("AccentOnDarkBrush") as Brush
            ?? Brushes.CornflowerBlue;
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            AppendLine(textBlock, lines[lineIndex], linkBrush);
            if (lineIndex < lines.Length - 1)
            {
                textBlock.Inlines.Add(new LineBreak());
            }
        }
    }

    private static void AppendLine(TextBlock textBlock, string line, Brush linkBrush)
    {
        var cursor = 0;

        foreach (Match match in UrlPattern.Matches(line))
        {
            var linkText = TrimTrailingPunctuation(match.Value);
            if (linkText.Length == 0)
            {
                continue;
            }

            if (match.Index > cursor)
            {
                textBlock.Inlines.Add(new Run(line[cursor..match.Index]));
            }

            var target = linkText.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? "https://" + linkText
                : linkText;

            if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var hyperlink = new Hyperlink(new Run(linkText))
                {
                    NavigateUri = uri,
                    Foreground = linkBrush,
                    TextDecorations = TextDecorations.Underline,
                    Cursor = Cursors.Hand,
                    ToolTip = uri.AbsoluteUri
                };
                hyperlink.RequestNavigate += OnRequestNavigate;
                textBlock.Inlines.Add(hyperlink);
            }
            else
            {
                textBlock.Inlines.Add(new Run(linkText));
            }

            // 末尾被裁掉的标点仍作为普通文本保留。
            cursor = match.Index + linkText.Length;
        }

        if (cursor < line.Length)
        {
            textBlock.Inlines.Add(new Run(line[cursor..]));
        }
    }

    private static string TrimTrailingPunctuation(string value)
    {
        var end = value.Length;
        while (end > 0)
        {
            var last = value[end - 1];
            if (".,;:!?，。；：！？\"'”’".Contains(last))
            {
                end--;
                continue;
            }

            if (last == ')' && Count(value, ')') > Count(value, '('))
            {
                end--;
                continue;
            }

            if (last == ']' && Count(value, ']') > Count(value, '['))
            {
                end--;
                continue;
            }

            if (last == '}' && Count(value, '}') > Count(value, '{'))
            {
                end--;
                continue;
            }

            if ("）】》」".Contains(last))
            {
                end--;
                continue;
            }

            break;
        }

        return value[..end];
    }

    private static int Count(string value, char target) =>
        value.Count(character => character == target);

    private static void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;

        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 无默认浏览器或系统拒绝打开时保持界面稳定。
        }
    }
}
