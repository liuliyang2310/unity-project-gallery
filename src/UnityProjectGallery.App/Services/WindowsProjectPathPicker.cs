using System.Windows;
using Microsoft.Win32;

namespace UnityProjectGallery.App.Services;

/// <summary>使用 Windows 文件夹选择框定位移动后的工程。</summary>
public sealed class WindowsProjectPathPicker : IProjectPathPicker
{
    public string? PickProjectDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择移动后的 Unity 工程目录",
            Multiselect = false
        };

        var owner = Application.Current?.MainWindow;
        var accepted = owner is not null && owner.IsVisible
            ? dialog.ShowDialog(owner)
            : dialog.ShowDialog();

        return accepted == true ? dialog.FolderName : null;
    }
}
