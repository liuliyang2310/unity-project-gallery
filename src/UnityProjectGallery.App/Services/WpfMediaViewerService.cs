using System.Windows;
using UnityProjectGallery.App.Views;

namespace UnityProjectGallery.App.Services;

/// <summary>使用独立 WPF 窗口显示图片与视频。</summary>
public sealed class WpfMediaViewerService : IMediaViewerService
{
    public void ShowImage(string filePath, string title)
    {
        Show(filePath, title, isVideo: false);
    }

    public void ShowVideo(string filePath, string title)
    {
        Show(filePath, title, isVideo: true);
    }

    private static void Show(string filePath, string title, bool isVideo)
    {
        var window = new MediaViewerWindow(filePath, title, isVideo);
        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }
}
