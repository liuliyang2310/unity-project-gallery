using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace UnityProjectGallery.App.Services;

/// <summary>使用 System.Diagnostics.Process 启动 Unity Editor 或 Unity Hub。</summary>
public sealed class WindowsProcessLauncher : IExternalProcessLauncher
{
    public bool Start(string executablePath, IReadOnlyList<string> arguments, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = false
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process.Start(startInfo);
            return true;
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
