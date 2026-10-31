using System.IO;

namespace UnityProjectGallery.App.Services;

/// <summary>在常见安装位置查找 Unity Hub。</summary>
public sealed class WindowsUnityHubLocator : IUnityHubLocator
{
    public string? FindExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("UNITY_HUB_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Unity Hub", "Unity Hub.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Unity Hub", "Unity Hub.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Unity Hub", "Unity Hub.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Unity", "Hub", "Unity Hub.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Unity", "Hub", "Unity Hub.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
