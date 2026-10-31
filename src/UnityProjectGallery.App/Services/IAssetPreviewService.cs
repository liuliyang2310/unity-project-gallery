namespace UnityProjectGallery.App.Services;

/// <summary>打开工程的 Assets 资源预览窗口。</summary>
public interface IAssetPreviewService
{
    void ShowAssetBrowser(string projectName, string projectPath);
}
