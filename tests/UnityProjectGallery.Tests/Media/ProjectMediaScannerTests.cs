using System.Diagnostics;
using System.IO;
using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Infrastructure.Media;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Tests.Media;

public sealed class ProjectMediaScannerTests : IDisposable
{
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lK3Q6wAAAABJRU5ErkJggg==";

    private readonly string _tempRoot;
    private readonly string _projectPath;
    private readonly string _cachePath;

    public ProjectMediaScannerTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "UnityProjectGalleryMediaTests",
            Guid.NewGuid().ToString("N"));
        _projectPath = Path.Combine(_tempRoot, "MediaProject");
        _cachePath = Path.Combine(_tempRoot, "Cache");
        Directory.CreateDirectory(Path.Combine(_projectPath, ProjectMediaConventions.PreviewDirectoryName));
        Directory.CreateDirectory(_cachePath);
    }

    [Fact]
    public async Task ScanAsync_IndexesOnlyPreviewDirectoryAndBuildsThumbnail()
    {
        var previewDirectory = Path.Combine(_projectPath, ProjectMediaConventions.PreviewDirectoryName);
        CreateOnePixelPng(Path.Combine(previewDirectory, "cover.png"));
        CreateOnePixelPng(Path.Combine(previewDirectory, "nested", "second.png"));

        // 其他目录中的素材不应被索引。
        CreateOnePixelPng(Path.Combine(_projectPath, "Screenshots", "ignored.png"));
        Directory.CreateDirectory(Path.Combine(_projectPath, "Videos"));
        File.WriteAllText(Path.Combine(_projectPath, "Videos", "ignored.txt"), "text");

        var scanner = new ProjectMediaScanner(_cachePath);

        var result = await scanner.ScanAsync(_projectPath);

        Assert.Equal(2, result.Media.Count);
        Assert.All(result.Media, media =>
        {
            Assert.Equal(ProjectMediaKind.Image, media.Kind);
            Assert.Equal(1, media.PixelWidth);
            Assert.Equal(1, media.PixelHeight);
            Assert.NotNull(media.ThumbnailPath);
            Assert.True(File.Exists(media.ThumbnailPath));
            Assert.StartsWith(_cachePath, media.ThumbnailPath, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(result.Media, media => media.RelativePath == Path.Combine("nested", "second.png"));
        Assert.DoesNotContain(result.Media, media => media.FileName == "ignored.png");
    }

    [Fact]
    public async Task ScanAsync_HandlesCorruptImagesWithoutThrowing()
    {
        var previewDirectory = Path.Combine(_projectPath, ProjectMediaConventions.PreviewDirectoryName);
        File.WriteAllText(Path.Combine(previewDirectory, "broken.png"), "not an image");

        var scanner = new ProjectMediaScanner(_cachePath);

        var result = await scanner.ScanAsync(_projectPath);

        var asset = Assert.Single(result.Media);
        Assert.Equal(ProjectMediaKind.Image, asset.Kind);
        Assert.Null(asset.PixelWidth);
        Assert.Null(asset.PixelHeight);
        Assert.Null(asset.ThumbnailPath);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task ScanAsync_ReturnsEmptyWhenPreviewDirectoryIsMissing()
    {
        var projectWithoutPreview = Path.Combine(_tempRoot, "NoPreviewProject");
        Directory.CreateDirectory(projectWithoutPreview);

        var scanner = new ProjectMediaScanner(_cachePath);

        var result = await scanner.ScanAsync(projectWithoutPreview);

        Assert.Empty(result.Media);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ScanAsync_ReadsWebpImageWhenFfmpegIsAvailable()
    {
        if (FindExecutable("ffmpeg") is null)
        {
            return;
        }

        var previewDirectory = Path.Combine(_projectPath, ProjectMediaConventions.PreviewDirectoryName);
        var webpPath = Path.Combine(previewDirectory, "preview.webp");
        var created = await TryRunProcessAsync(
            "ffmpeg",
            "-y", "-f", "lavfi", "-i", "color=c=red:s=320x200", "-frames:v", "1", webpPath);
        if (!created)
        {
            return;
        }

        var scanner = new ProjectMediaScanner(_cachePath);

        var result = await scanner.ScanAsync(_projectPath);

        var asset = Assert.Single(result.Media);
        Assert.Equal(ProjectMediaKind.Image, asset.Kind);
        Assert.Equal(320, asset.PixelWidth);
        Assert.Equal(200, asset.PixelHeight);
        Assert.NotNull(asset.ThumbnailPath);
    }

    [Fact]
    public async Task ScanAsync_ReadsVideoMetadataAndGeneratesCoverWhenFfmpegIsAvailable()
    {
        if (FindExecutable("ffmpeg") is null || FindExecutable("ffprobe") is null)
        {
            return;
        }

        var previewDirectory = Path.Combine(_projectPath, ProjectMediaConventions.PreviewDirectoryName);
        var videoPath = Path.Combine(previewDirectory, "demo.mp4");
        var created = await TryRunProcessAsync(
            "ffmpeg",
            "-y",
            "-f", "lavfi",
            "-i", "testsrc=size=320x240:rate=10",
            "-t", "1",
            "-pix_fmt", "yuv420p",
            videoPath);
        if (!created)
        {
            return;
        }

        var scanner = new ProjectMediaScanner(_cachePath);

        var result = await scanner.ScanAsync(_projectPath);

        var asset = Assert.Single(result.Media);
        Assert.Equal(ProjectMediaKind.Video, asset.Kind);
        Assert.NotNull(asset.DurationSeconds);
        Assert.InRange(asset.DurationSeconds.Value, 0.8, 2.0);
        Assert.NotNull(asset.PixelWidth);
        Assert.NotNull(asset.PixelHeight);
        Assert.NotNull(asset.ThumbnailPath);
        Assert.True(File.Exists(asset.ThumbnailPath));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void CreateOnePixelPng(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, Convert.FromBase64String(OnePixelPngBase64));
    }

    private static string? FindExecutable(string name)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (pathVariable is null)
        {
            return null;
        }

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), name + ".exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<bool> TryRunProcessAsync(string executable, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (
            exception is IOException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
