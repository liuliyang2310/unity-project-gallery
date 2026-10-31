using System.Windows;
using UnityProjectGallery.App.Services;

namespace UnityProjectGallery.App.Views;

public partial class UnityEditorMissingWindow : Window
{
    public UnityEditorMissingWindow(
        string projectName,
        string projectPath,
        string unityVersion)
    {
        InitializeComponent();

        VersionText.Text = $"工程使用的 Unity 版本：{unityVersion}";
        ProjectText.Text = $"{projectName} · {projectPath}";
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Escape)
            {
                Choice = UnityEditorMissingChoice.Cancel;
                Close();
            }
        };
    }

    public UnityEditorMissingChoice Choice { get; private set; } =
        UnityEditorMissingChoice.Cancel;

    private void OnChooseEditorClicked(object sender, RoutedEventArgs e)
    {
        Choice = UnityEditorMissingChoice.ChooseEditor;
        Close();
    }

    private void OnUseHubClicked(object sender, RoutedEventArgs e)
    {
        Choice = UnityEditorMissingChoice.UseUnityHub;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        Choice = UnityEditorMissingChoice.Cancel;
        Close();
    }
}
