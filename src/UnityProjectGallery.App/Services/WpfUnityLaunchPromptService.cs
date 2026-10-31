using System.Windows;
using Microsoft.Win32;
using UnityProjectGallery.App.Views;

namespace UnityProjectGallery.App.Services;

/// <summary>使用深色 WPF 窗口询问缺失编辑器时的选择，并打开 Unity.exe 选择框。</summary>
public sealed class WpfUnityLaunchPromptService : IUnityLaunchPromptService
{
    public UnityEditorMissingChoice AskForMissingEditor(
        string projectName,
        string projectPath,
        string unityVersion)
    {
        var window = new UnityEditorMissingWindow(projectName, projectPath, unityVersion);
        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
        return window.Choice;
    }

    public string? PickEditorExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 Unity Editor",
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
