using System.IO;
using UnityProjectGallery.Core.Models;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Infrastructure.Models;

/// <summary>异步枚举 `<工程>/Assets` 下可预览的模型、图片与音频文件。</summary>
public sealed class AssetsPreviewScanner : IAssetsPreviewScanner
{
    private const int MaxDepth = 40;

    public Task<IReadOnlyList<AssetPreviewInfo>> ScanAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(projectPath, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<AssetPreviewInfo> Scan(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var assetsPath = Path.Combine(projectPath, "Assets");
        if (!Directory.Exists(assetsPath))
        {
            return Array.Empty<AssetPreviewInfo>();
        }

        var results = new List<AssetPreviewInfo>();
        Enumerate(assetsPath, assetsPath, results, cancellationToken, 0);

        return results
            .OrderBy(asset => asset.Kind)
            .ThenBy(asset => asset.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void Enumerate(
        string directory,
        string assetsRoot,
        List<AssetPreviewInfo> results,
        CancellationToken cancellationToken,
        int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            files = Array.Empty<string>();
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!AssetPreviewConventions.TryGetKind(file, out var kind))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(file);
                results.Add(new AssetPreviewInfo(
                    file,
                    Path.GetRelativePath(assetsRoot, file),
                    Path.GetFileName(file),
                    kind,
                    info.Length,
                    info.LastWriteTime));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var childDirectory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSkippedDirectory(childDirectory))
            {
                continue;
            }

            try
            {
                var attributes = File.GetAttributes(childDirectory);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            Enumerate(childDirectory, assetsRoot, results, cancellationToken, depth + 1);
        }
    }

    private static bool IsSkippedDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("Library", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Logs", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("obj", StringComparison.OrdinalIgnoreCase);
    }
}
