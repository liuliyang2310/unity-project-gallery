using System.Diagnostics;
using System.IO;

namespace UnityProjectGallery.App.Services;

/// <summary>使用默认资源管理器打开文件夹。</summary>
public sealed class WindowsExplorerRevealService : IProjectFolderRevealService
{
    public bool Reveal(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(folderPath)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            IOException)
        {
            return false;
        }
    }
}
