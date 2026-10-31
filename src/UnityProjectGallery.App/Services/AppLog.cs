using System.IO;
using System.Reflection;
using System.Text;

namespace UnityProjectGallery.App.Services;

/// <summary>
/// 轻量文件日志。日志写入 `%LOCALAPPDATA%\UnityProjectGallery\Logs`，
/// 按天分文件，保留最近 14 天，写入失败时静默忽略，避免日志影响主流程。
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static readonly string LogFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UnityProjectGallery",
        "Logs");

    public static string LogDirectory => LogFolder;

    public static string AppVersion
    {
        get
        {
            try
            {
                var informational = Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(informational))
                {
                    return informational;
                }

                return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                    ?? "unknown";
            }
            catch (Exception)
            {
                return "unknown";
            }
        }
    }

    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            CleanupOldLogs();
            Info($"应用启动 · 版本 {AppVersion} · Windows {Environment.OSVersion.Version}");
        }
        catch (Exception)
        {
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        Write(
            "ERROR",
            exception is null
                ? message
                : $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogFolder);
                var logPath = Path.Combine(
                    LogFolder,
                    $"unity-project-gallery-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(
                    logPath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}",
                    Utf8NoBom);
            }
        }
        catch (Exception)
        {
        }
    }

    private static void CleanupOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-14);
            foreach (var file in Directory.GetFiles(
                         LogFolder,
                         "unity-project-gallery-*.log",
                         SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }
}
