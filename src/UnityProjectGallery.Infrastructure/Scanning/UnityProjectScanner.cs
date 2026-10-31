using System.IO;
using System.Text.Json;
using UnityProjectGallery.Core.Scanning;
using UnityProjectGallery.Infrastructure.Common;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Infrastructure.Scanning;

/// <summary>
/// 只读扫描本机目录中的 Unity 工程。
/// 扫描在后台线程执行，通过 <see cref="IProgress{T}"/> 报告进度，并支持取消。
/// </summary>
public sealed class UnityProjectScanner : IUnityProjectScanner
{
    private static readonly string[] DefaultExcludedDirectories =
    {
        "Library", "Temp", "Logs", "Obj", "Build", "Builds",
        "MemoryCaptures", "Recordings", "UserSettings", "node_modules",
        ".git", ".svn", ".vs", ".idea"
    };

    private readonly HashSet<string> _excludedDirectories;

    public UnityProjectScanner(IEnumerable<string>? excludedDirectoryNames = null)
    {
        _excludedDirectories = new HashSet<string>(
            excludedDirectoryNames ?? DefaultExcludedDirectories,
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<UnityProject>> ScanAsync(
        IEnumerable<string> rootPaths,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootPaths);

        return Task.Run<IReadOnlyList<UnityProject>>(
            () => Scan(rootPaths, progress, cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<UnityProject> Scan(
        IEnumerable<string> rootPaths,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roots = NormalizeRoots(rootPaths, cancellationToken);

        progress?.Report(new ScanProgress(
            ScanPhase.Starting, 0, roots.Count, 0, null, "正在准备扫描…"));

        if (roots.Count == 0)
        {
            progress?.Report(new ScanProgress(
                ScanPhase.Completed, 0, 0, 0, null,
                "没有可扫描的目录"));
            return Array.Empty<UnityProject>();
        }

        var context = new ScanContext(progress);
        var results = new List<UnityProject>();
        var foundProjectKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < roots.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rootIndex = index + 1;
            var rootPath = roots[index];
            context.ReportRoot(rootIndex, roots.Count, rootPath, results.Count);

            if (!Directory.Exists(rootPath))
            {
                context.ReportSkippedRoot(rootIndex, roots.Count, rootPath, results.Count,
                    "扫描目录不存在，已跳过");
                continue;
            }

            ScanRoot(
                rootPath,
                rootIndex,
                roots.Count,
                visitedDirectories,
                foundProjectKeys,
                results,
                context,
                cancellationToken);
        }

        progress?.Report(new ScanProgress(
            ScanPhase.Completed,
            roots.Count,
            roots.Count,
            results.Count,
            null,
            $"扫描完成，共发现 {results.Count} 个 Unity 工程"));

        return results;
    }

    private static List<string> NormalizeRoots(
        IEnumerable<string> rootPaths,
        CancellationToken cancellationToken)
    {
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in rootPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            try
            {
                var normalized = ProjectPathHelper.Normalize(path);
                if (seen.Add(normalized))
                {
                    roots.Add(normalized);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or IOException or NotSupportedException)
            {
                // 无效路径直接忽略，避免中断整次扫描。
            }
        }

        return roots;
    }

    private void ScanRoot(
        string rootPath,
        int rootIndex,
        int rootCount,
        HashSet<string> visitedDirectories,
        HashSet<string> foundProjectKeys,
        List<UnityProject> results,
        ScanContext context,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = pending.Pop();

            if (!visitedDirectories.Add(currentDirectory))
            {
                continue;
            }

            if (IsUnityProjectDirectory(currentDirectory))
            {
                var project = ReadProject(currentDirectory);
                if (foundProjectKeys.Add(project.ProjectPath))
                {
                    results.Add(project);
                }

                context.ReportProject(rootIndex, rootCount, results.Count, project);
                // 已识别为工程根目录后不再深入，避免扫描 Library 等缓存与重复工程。
                continue;
            }

            foreach (var childDirectory in EnumerateChildDirectories(currentDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!visitedDirectories.Contains(childDirectory))
                {
                    pending.Push(childDirectory);
                }
            }

            context.ReportDirectory(rootIndex, rootCount, results.Count, currentDirectory);
        }
    }

    private IEnumerable<string> EnumerateChildDirectories(string directory)
    {
        string[] children;
        try
        {
            children = Directory.GetDirectories(directory);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return Array.Empty<string>();
        }

        var directories = new List<string>(children.Length);
        foreach (var child in children)
        {
            var name = Path.GetFileName(child);
            if (_excludedDirectories.Contains(name))
            {
                continue;
            }

            try
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                {
                    // 跳过符号链接 / Junction，避免循环扫描。
                    continue;
                }
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            directories.Add(child);
        }

        return directories;
    }

    private static bool IsUnityProjectDirectory(string directory)
    {
        return Directory.Exists(Path.Combine(directory, "Assets"))
            && Directory.Exists(Path.Combine(directory, "Packages"))
            && Directory.Exists(Path.Combine(directory, "ProjectSettings"));
    }

    private static UnityProject ReadProject(string directory)
    {
        var normalizedPath = ProjectPathHelper.Normalize(directory);
        var name = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = normalizedPath;
        }

        DateTimeOffset? lastModified = null;
        try
        {
            lastModified = new DateTimeOffset(Directory.GetLastWriteTime(normalizedPath));
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
        }

        return new UnityProject
        {
            Id = normalizedPath,
            Name = name,
            ProjectPath = normalizedPath,
            UnityVersion = ReadUnityVersion(normalizedPath) ?? "未识别",
            Packages = ReadPackages(normalizedPath),
            LastModifiedTime = lastModified,
            LastScanTime = DateTimeOffset.Now,
            ScreenshotCount = 0,
            VideoCount = 0,
            FeatureCount = 0
        };
    }

    private static string? ReadUnityVersion(string projectPath)
    {
        var versionFile = Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
        try
        {
            if (!File.Exists(versionFile))
            {
                return null;
            }

            foreach (var line in File.ReadLines(versionFile))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("m_EditorVersion:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var version = trimmed["m_EditorVersion:".Length..].Trim();
                return string.IsNullOrWhiteSpace(version) ? null : version;
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException)
        {
        }

        return null;
    }

    private static UnityPackageReference[] ReadPackages(string projectPath)
    {
        var manifestFile = Path.Combine(projectPath, "Packages", "manifest.json");
        var packages = new List<UnityPackageReference>();

        try
        {
            if (!File.Exists(manifestFile))
            {
                return packages.ToArray();
            }

            using var document = JsonDocument.Parse(File.ReadAllText(manifestFile));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("dependencies", out var dependencies) ||
                dependencies.ValueKind != JsonValueKind.Object)
            {
                return packages.ToArray();
            }

            foreach (var dependency in dependencies.EnumerateObject())
            {
                if (dependency.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                packages.Add(new UnityPackageReference
                {
                    Name = dependency.Name,
                    Version = dependency.Value.GetString() ?? string.Empty
                });
            }
        }
        catch (Exception exception) when (
            exception is JsonException or UnauthorizedAccessException or IOException)
        {
            // manifest.json 损坏时保留工程识别结果，仅跳过依赖信息。
        }

        packages.Sort((left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
        return packages.ToArray();
    }

    private sealed class ScanContext
    {
        private const long ReportIntervalMilliseconds = 120;
        private readonly IProgress<ScanProgress>? _progress;
        private long _lastReportTicks = Environment.TickCount64 - ReportIntervalMilliseconds;

        public ScanContext(IProgress<ScanProgress>? progress)
        {
            _progress = progress;
        }

        public void ReportRoot(int rootIndex, int rootCount, string rootPath, int projectsFound)
        {
            _lastReportTicks = Environment.TickCount64;
            _progress?.Report(new ScanProgress(
                ScanPhase.Scanning,
                rootIndex,
                rootCount,
                projectsFound,
                rootPath,
                $"正在扫描第 {rootIndex}/{rootCount} 个目录"));
        }

        public void ReportSkippedRoot(
            int rootIndex,
            int rootCount,
            string rootPath,
            int projectsFound,
            string message)
        {
            _lastReportTicks = Environment.TickCount64;
            _progress?.Report(new ScanProgress(
                ScanPhase.Scanning,
                rootIndex,
                rootCount,
                projectsFound,
                rootPath,
                message));
        }

        public void ReportProject(
            int rootIndex,
            int rootCount,
            int projectsFound,
            UnityProject project)
        {
            _lastReportTicks = Environment.TickCount64;
            _progress?.Report(new ScanProgress(
                ScanPhase.Scanning,
                rootIndex,
                rootCount,
                projectsFound,
                project.ProjectPath,
                $"发现工程：{project.Name}"));
        }

        public void ReportDirectory(
            int rootIndex,
            int rootCount,
            int projectsFound,
            string directory)
        {
            var now = Environment.TickCount64;
            if (now - _lastReportTicks < ReportIntervalMilliseconds)
            {
                return;
            }

            _lastReportTicks = now;
            _progress?.Report(new ScanProgress(
                ScanPhase.Scanning,
                rootIndex,
                rootCount,
                projectsFound,
                directory,
                "正在扫描目录结构"));
        }
    }
}
