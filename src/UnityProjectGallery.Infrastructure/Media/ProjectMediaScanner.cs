using System.IO;
using System.Windows.Media.Imaging;
using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Infrastructure.Common;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Infrastructure.Media;

/// <summary>
/// 仅扫描工程根目录下的 `_ProjectPreview`。
/// 图片与视频缩略图写入软件本地缓存，不修改工程内任何文件。
/// </summary>
public sealed class ProjectMediaScanner : IProjectMediaScanner
{
    private const int ThumbnailMaxSize = 480;

    private readonly MediaThumbnailCache _cache;
    private readonly FFmpegMediaTools _ffmpeg;

    public ProjectMediaScanner(
        string? cacheDirectory = null,
        string? ffmpegPath = null,
        string? ffprobePath = null)
    {
        _cache = new MediaThumbnailCache(cacheDirectory);
        _ffmpeg = new FFmpegMediaTools(ffmpegPath, ffprobePath);
    }

    public Task<ProjectMediaScanResult> ScanAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var normalizedProjectPath = ProjectPathHelper.Normalize(projectPath);

        return Task.Run(
            () => ScanCoreAsync(normalizedProjectPath, cancellationToken),
            cancellationToken);
    }

    private async Task<ProjectMediaScanResult> ScanCoreAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var previewDirectory = Path.Combine(
            projectPath,
            ProjectMediaConventions.PreviewDirectoryName);

        if (!Directory.Exists(previewDirectory))
        {
            return new ProjectMediaScanResult(
                projectPath,
                Array.Empty<ProjectMediaAsset>(),
                Array.Empty<string>(),
                _ffmpeg.IsAvailable);
        }

        var warnings = new List<string>();
        var assets = new List<ProjectMediaAsset>();
        var ffmpegWarningAdded = false;

        string[] files;
        try
        {
            files = Directory.GetFiles(previewDirectory, "*", SearchOption.AllDirectories);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
            warnings.Add($"无法读取 _ProjectPreview：{exception.Message}");
            return new ProjectMediaScanResult(projectPath, assets, warnings, _ffmpeg.IsAvailable);
        }

        var supportedFiles = files
            .Where(file =>
                ProjectMediaConventions.IsSupportedImage(file) ||
                ProjectMediaConventions.IsSupportedVideo(file))
            .OrderBy(file => Path.GetRelativePath(previewDirectory, file), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var filePath in supportedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var kind = ProjectMediaConventions.IsSupportedVideo(filePath)
                ? ProjectMediaKind.Video
                : ProjectMediaKind.Image;
            var fileName = Path.GetFileName(filePath);
            var relativePath = Path.GetRelativePath(previewDirectory, filePath);

            int? pixelWidth = null;
            int? pixelHeight = null;
            double? durationSeconds = null;
            string? thumbnailPath = null;

            try
            {
                var cachePath = _cache.GetThumbnailPath(projectPath, filePath, kind);

                if (kind == ProjectMediaKind.Image)
                {
                    var imageInfo = TryReadImageInfo(filePath);
                    if (imageInfo is not null)
                    {
                        pixelWidth = imageInfo.Value.Width;
                        pixelHeight = imageInfo.Value.Height;
                    }
                    else if (_ffmpeg.IsAvailable)
                    {
                        var probe = await _ffmpeg.ProbeAsync(filePath, cancellationToken);
                        if (probe is not null)
                        {
                            pixelWidth = probe.Width is > 0 ? probe.Width : null;
                            pixelHeight = probe.Height is > 0 ? probe.Height : null;
                        }
                    }

                    if (IsThumbnailFresh(filePath, cachePath))
                    {
                        thumbnailPath = cachePath;
                    }
                    else if (TryCreateImageThumbnail(filePath, cachePath, pixelWidth, pixelHeight))
                    {
                        thumbnailPath = cachePath;
                    }
                    else if (_ffmpeg.IsAvailable &&
                             await _ffmpeg.CreateImageThumbnailAsync(filePath, cachePath, cancellationToken))
                    {
                        thumbnailPath = cachePath;
                    }
                    else
                    {
                        warnings.Add($"无法生成图片缩略图：{relativePath}");
                    }

                    if (pixelWidth is null || pixelHeight is null)
                    {
                        warnings.Add($"无法读取图片尺寸：{relativePath}");
                    }
                }
                else
                {
                    if (!_ffmpeg.IsAvailable)
                    {
                        if (!ffmpegWarningAdded)
                        {
                            warnings.Add("未找到 FFmpeg / FFprobe，视频时长与封面无法生成。");
                            ffmpegWarningAdded = true;
                        }
                    }
                    else
                    {
                        var probe = await _ffmpeg.ProbeAsync(filePath, cancellationToken);
                        if (probe is not null)
                        {
                            pixelWidth = probe.Width is > 0 ? probe.Width : null;
                            pixelHeight = probe.Height is > 0 ? probe.Height : null;
                            durationSeconds = probe.DurationSeconds is > 0
                                ? probe.DurationSeconds
                                : null;
                        }
                        else
                        {
                            warnings.Add($"无法读取视频信息：{relativePath}");
                        }

                        if (IsThumbnailFresh(filePath, cachePath))
                        {
                            thumbnailPath = cachePath;
                        }
                        else if (await _ffmpeg.CreateVideoCoverAsync(
                                     filePath,
                                     cachePath,
                                     durationSeconds,
                                     cancellationToken))
                        {
                            thumbnailPath = cachePath;
                        }
                        else
                        {
                            warnings.Add($"无法生成视频封面：{relativePath}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"无法处理素材 {relativePath}：{exception.Message}");
            }

            assets.Add(new ProjectMediaAsset
            {
                FilePath = filePath,
                FileName = fileName,
                Kind = kind,
                RelativePath = relativePath,
                PixelWidth = pixelWidth,
                PixelHeight = pixelHeight,
                DurationSeconds = durationSeconds,
                ThumbnailPath = thumbnailPath is not null && File.Exists(thumbnailPath)
                    ? thumbnailPath
                    : null
            });
        }

        return new ProjectMediaScanResult(
            projectPath,
            assets,
            warnings.Distinct(StringComparer.CurrentCulture).ToArray(),
            _ffmpeg.IsAvailable);
    }

    private static (int Width, int Height)? TryReadImageInfo(string filePath)
    {
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return null;
            }

            var frame = decoder.Frames[0];
            return frame.PixelWidth > 0 && frame.PixelHeight > 0
                ? (frame.PixelWidth, frame.PixelHeight)
                : null;
        }
        catch (Exception exception) when (
            exception is NotSupportedException or FileFormatException or IOException or ArgumentException)
        {
            return null;
        }
    }

    private static bool TryCreateImageThumbnail(
        string sourcePath,
        string destinationPath,
        int? width,
        int? height)
    {
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(destinationPath) ?? Path.GetTempPath(),
            Path.GetFileNameWithoutExtension(destinationPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            var bitmap = new BitmapImage();
            using (var stream = new FileStream(
                       sourcePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;

                if (width is > 0 && height is > 0)
                {
                    if (width >= height && width > ThumbnailMaxSize)
                    {
                        bitmap.DecodePixelWidth = ThumbnailMaxSize;
                    }
                    else if (height > ThumbnailMaxSize)
                    {
                        bitmap.DecodePixelHeight = ThumbnailMaxSize;
                    }
                }

                bitmap.EndInit();
            }

            bitmap.Freeze();

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");

            using (var output = File.Create(temporaryPath))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(output);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (
            exception is NotSupportedException or FileFormatException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private static bool IsThumbnailFresh(string sourcePath, string thumbnailPath)
    {
        try
        {
            return File.Exists(thumbnailPath) &&
                   File.GetLastWriteTimeUtc(thumbnailPath) >= File.GetLastWriteTimeUtc(sourcePath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
