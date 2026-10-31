using Microsoft.Win32;
using System.Windows;

namespace UnityProjectGallery.App.Services;

/// <summary>使用系统文件选择框选取 Unity.exe。</summary>
public sealed class WindowsUnityEditorFilePicker : IUnityEditorFilePicker
{
    public string? PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 Unity Editor（Unity.exe）",
            Filter = "Unity Editor (Unity.exe)|Unity.exe|可执行文件 (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        var owner = Application.Current?.MainWindow;
        var accepted = owner is not null && owner.IsVisible
            ? dialog.ShowDialog(owner)
            : dialog.ShowDialog();

        return accepted == true ? dialog.FileName : null;
    }
}
