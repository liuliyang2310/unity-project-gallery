using System.Windows;
using UnityProjectGallery.App.ViewModels;
using UnityProjectGallery.App.Views;
using UnityProjectGallery.Core.Models;

namespace UnityProjectGallery.App.Services;

/// <summary>使用独立 WPF 窗口预览工程 Assets 中的模型、图片与音频。</summary>
public sealed class WpfAssetPreviewService : IAssetPreviewService
{
    private readonly IAssetsPreviewScanner _scanner;
    private readonly IModelPreviewLoader _loader;
    private readonly IMediaViewerService _mediaViewer;
    private readonly IProjectFolderRevealService _folderRevealService;

    public WpfAssetPreviewService(
        IAssetsPreviewScanner scanner,
        IModelPreviewLoader loader,
        IMediaViewerService mediaViewer,
        IProjectFolderRevealService folderRevealService)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(mediaViewer);
        ArgumentNullException.ThrowIfNull(folderRevealService);
        _scanner = scanner;
        _loader = loader;
        _mediaViewer = mediaViewer;
        _folderRevealService = folderRevealService;
    }

    public void ShowAssetBrowser(string projectName, string projectPath)
    {
        var viewModel = new AssetPreviewViewModel(
            _scanner,
            _loader,
            _mediaViewer,
            _folderRevealService,
            projectName,
            projectPath);
        var window = new AssetPreviewWindow { DataContext = viewModel };
        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }
}
