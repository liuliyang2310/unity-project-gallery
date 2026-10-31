using System.Windows;
using Microsoft.Win32;

namespace UnityProjectGallery.App.Services;

/// <summary>基于 WPF OpenFolderDialog 的目录选择实现。</summary>
public sealed class WindowsScanFolderPicker : IScanFolderPicker
{
    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择包含 Unity 工程的目录",
            Multiselect = false
        };

        var owner = Application.Current?.MainWindow;
        var accepted = owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(owner);

        return accepted == true ? dialog.FolderName : null;
    }
}
