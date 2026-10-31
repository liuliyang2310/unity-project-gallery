using System.Diagnostics;
using System.IO;
using System.Text.Json;
using UnityProjectGallery.Core.Editors;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Infrastructure.Editors;

/// <summary>
/// 从 Unity Hub 默认安装目录、旧版独立安装目录与 Hub 自定义安装根目录中发现 Unity Editor。
/// </summary>
public sealed class UnityEditorLocator : IUnityEditorLocator
{
    private readonly IReadOnlyList<string>? _editorRoots;

    public UnityEditorLocator(IEnumerable<string>? editorRoots = null)
    {
        _editorRoots = editorRoots?.ToArray();
    }

    public Task<IReadOnlyList<UnityEditorInstallation>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<UnityEditorInstallation>>(
            () => Discover(cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<UnityEditorInstallation> Discover(CancellationToken cancellationToken)
    {
        var roots = _editorRoots ?? GetDefaultRoots();
        var installations = new Dictionary<string, UnityEditorInstallation>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            var directEditor = Path.Combine(root, "Unity.exe");
            if (File.Exists(directEditor))
            {
                AddInstallation(installations, ReadVersion(directEditor, root), directEditor);
            }

            string[] versionDirectories;
            try
            {
                versionDirectories = Directory.GetDirectories(root);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var versionDirectory in versionDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var version = Path.GetFileName(versionDirectory);
                var candidates = new[]
                {
                    Path.Combine(versionDirectory, "Editor", "Unity.exe"),
                    Path.Combine(versionDirectory, "Unity.exe")
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        AddInstallation(installations, version, candidate);
                        break;
                    }
                }
            }
        }

        return installations.Values
            .OrderByDescending(installation => installation.Version, StringComparer.OrdinalIgnoreCase)
            .ThenBy(installation => installation.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddInstallation(
        IDictionary<string, UnityEditorInstallation> installations,
        string version,
        string executablePath)
    {
        var normalizedVersion = version.Trim();
        if (normalizedVersion.Length == 0 || installations.ContainsKey(executablePath))
        {
            return;
        }

        installations[executablePath] = new UnityEditorInstallation
        {
            Version = normalizedVersion,
            ExecutablePath = executablePath
        };
    }

    private static string ReadVersion(string executablePath, string fallbackRoot)
    {
        try
        {
            var productVersion = FileVersionInfo
                .GetVersionInfo(executablePath)
                .ProductVersion
                ?.Trim();
            if (!string.IsNullOrWhiteSpace(productVersion))
            {
                return productVersion;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }

        return Path.GetFileName(fallbackRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));
    }

    private static string[] GetDefaultRoots()
    {
        var roots = new List<string>();

        AddRoot(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Unity", "Hub", "Editor"));
        AddRoot(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Unity", "Hub", "Editor"));
        AddRoot(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Unity", "Hub", "Editor"));
        AddRoot(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Unity", "Editor"));
        AddRoot(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Unity", "Editor"));

        var secondaryRoot = ReadHubSecondaryInstallRoot();
        if (!string.IsNullOrWhiteSpace(secondaryRoot))
        {
            AddRoot(secondaryRoot);
        }

        return roots.ToArray();

        void AddRoot(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !roots.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(path);
            }
        }
    }

    private static string? ReadHubSecondaryInstallRoot()
    {
        var configFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UnityHub",
            "secondaryInstallPath.json");

        try
        {
            if (!File.Exists(configFile))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(configFile));
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString()
                : null;
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
