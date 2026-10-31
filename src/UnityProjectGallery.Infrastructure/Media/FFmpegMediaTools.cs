using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace UnityProjectGallery.Infrastructure.Media;

/// <summary>
/// 封装 FFmpeg / FFprobe 的探测与封面提取。缺少可执行文件时以可恢复状态返回。
/// </summary>
public sealed class FFmpegMediaTools
{
    private readonly string? _ffmpegPath;
    private readonly string? _ffprobePath;

    public FFmpegMediaTools(string? ffmpegPath = null, string? ffprobePath = null)
    {
        _ffmpegPath = ResolveExecutable("ffmpeg", ffmpegPath);
        _ffprobePath = ResolveExecutable("ffprobe", ffprobePath ?? DirectoryOf(ffmpegPath));
    }

    public bool IsAvailable => _ffmpegPath is not null && _ffprobePath is not null;

    public string? FFmpegPath => _ffmpegPath;

    public async Task<FFmpegProbeResult?> ProbeAsync(string filePath, CancellationToken cancellationToken)
    {
        if (_ffprobePath is null)
        {
            return null;
        }

        var result = await RunAsync(
            _ffprobePath,
            new[]
            {
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "format=duration:stream=width,height",
                "-of", "json",
                filePath
            },
            cancellationToken);

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            int? width = null;
            int? height = null;
            if (root.TryGetProperty("streams", out var streams) &&
                streams.ValueKind == JsonValueKind.Array &&
                streams.GetArrayLength() > 0)
            {
                var stream = streams[0];
                if (stream.TryGetProperty("width", out var widthElement) &&
                    widthElement.TryGetInt32(out var parsedWidth))
                {
                    width = parsedWidth;
                }

                if (stream.TryGetProperty("height", out var heightElement) &&
                    heightElement.TryGetInt32(out var parsedHeight))
                {
                    height = parsedHeight;
                }
            }

            double? duration = null;
            if (root.TryGetProperty("format", out var format) &&
                format.ValueKind == JsonValueKind.Object &&
                format.TryGetProperty("duration", out var durationElement))
            {
                var durationText = durationElement.GetString();
                if (double.TryParse(
                        durationText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var parsedDuration))
                {
                    duration = parsedDuration;
                }
            }

            return new FFmpegProbeResult(width, height, duration);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task<bool> CreateImageThumbnailAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        return CreateThumbnailAsync(sourcePath, destinationPath, null, cancellationToken);
    }

    public Task<bool> CreateVideoCoverAsync(
        string videoPath,
        string destinationPath,
        double? durationSeconds,
        CancellationToken cancellationToken)
    {
        var seekSeconds = durationSeconds is > 0
            ? Math.Clamp(durationSeconds.Value * 0.1, 0.1, 3.0)
            : 0.0;
        return CreateThumbnailAsync(videoPath, destinationPath, seekSeconds, cancellationToken);
    }

    private async Task<bool> CreateThumbnailAsync(
        string sourcePath,
        string destinationPath,
        double? seekSeconds,
        CancellationToken cancellationToken)
    {
        if (_ffmpegPath is null)
        {
            return false;
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = Path.Combine(
            directory ?? Path.GetTempPath(),
            Path.GetFileNameWithoutExtension(destinationPath) + "." + Guid.NewGuid().ToString("N") + ".png");

        var arguments = new List<string>
        {
            "-nostdin", "-hide_banner", "-loglevel", "error", "-y"
        };

        if (seekSeconds is > 0)
        {
            arguments.Add("-ss");
            arguments.Add(seekSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        arguments.AddRange(new[]
        {
            "-i", sourcePath,
            "-frames:v", "1",
            "-vf", "scale=480:480:force_original_aspect_ratio=decrease",
            "-f", "image2",
            temporaryPath
        });

        try
        {
            var result = await RunAsync(_ffmpegPath, arguments, cancellationToken);
            if (result.ExitCode != 0 || !File.Exists(temporaryPath))
            {
                return false;
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static string? ResolveExecutable(string name, string? hint)
    {
        var fileName = OperatingSystem.IsWindows() ? name + ".exe" : name;

        if (!string.IsNullOrWhiteSpace(hint))
        {
            var fromHint = ResolveFromPath(hint, name, fileName);
            if (fromHint is not null)
            {
                return fromHint;
            }
        }

        var environmentHint = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(environmentHint))
        {
            var fromEnvironment = ResolveFromPath(environmentHint, name, fileName);
            if (fromEnvironment is not null)
            {
                return fromEnvironment;
            }
        }

        var localPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (pathVariable is null)
        {
            return null;
        }

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ResolveFromPath(string hint, string name, string fileName)
    {
        try
        {
            if (File.Exists(hint))
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(hint), name, StringComparison.OrdinalIgnoreCase))
                {
                    return hint;
                }

                var sibling = Path.Combine(Path.GetDirectoryName(hint) ?? string.Empty, fileName);
                return File.Exists(sibling) ? sibling : null;
            }

            var fromDirectory = Path.Combine(hint, fileName);
            return File.Exists(fromDirectory) ? fromDirectory : null;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException)
        {
            return null;
        }
    }

    private static string? DirectoryOf(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

/// <summary>FFprobe 返回的视频宽高与时长。</summary>
public sealed record FFmpegProbeResult(int? Width, int? Height, double? DurationSeconds);
