using System.IO;

namespace UnityProjectGallery.Infrastructure.Common;

/// <summary>统一工程路径的规范化与比较规则。</summary>
internal static class ProjectPathHelper
{
    public static string Normalize(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);

        if (root is not null && fullPath.Length > root.Length)
        {
            fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return fullPath;
    }

    public static string Key(string path) => Normalize(path).ToUpperInvariant();
}
