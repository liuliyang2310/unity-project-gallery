namespace UnityProjectGallery.Core.Media;

/// <summary>扫描单个工程 `_ProjectPreview` 目录中的图片与视频。</summary>
public interface IProjectMediaScanner
{
    Task<ProjectMediaScanResult> ScanAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}
