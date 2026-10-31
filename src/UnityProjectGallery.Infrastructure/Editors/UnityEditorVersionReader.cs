using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityProjectGallery.Core.Editors;

namespace UnityProjectGallery.Infrastructure.Editors;

/// <summary>
/// 从 Unity.exe 的 ProductVersion 读取版本；读取失败时回退到安装目录中的版本名。
/// </summary>
public sealed class UnityEditorVersionReader : IUnityEditorVersionReader
{
    private static readonly Regex VersionPattern = new(
        @"\d{4}\.\d+\.\d+(?:[a-zA-Z]\d+)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string? ReadVersion(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) ||
            !File.Exists(executablePath))
        {
            return null;
        }

        try
        {
            var productVersion = FileVersionInfo
                .GetVersionInfo(executablePath)
                .ProductVersion;
            var match = MatchVersion(productVersion);
            if (match is not null)
            {
                return match;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }

        var directory = Path.GetDirectoryName(executablePath);
        for (var depth = 0; depth < 3 && !string.IsNullOrWhiteSpace(directory); depth++)
        {
            var match = MatchVersion(Path.GetFileName(directory));
            if (match is not null)
            {
                return match;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static string? MatchVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = VersionPattern.Match(value);
        return match.Success ? match.Value : null;
    }
}
