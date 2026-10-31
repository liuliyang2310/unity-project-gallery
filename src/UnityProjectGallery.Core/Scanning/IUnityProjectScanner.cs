using UnityProjectGallery.Models;

namespace UnityProjectGallery.Core.Scanning;

/// <summary>
/// Unity 工程扫描入口。实现必须只读访问原始工程，并支持递归扫描、进度报告与取消。
/// </summary>
public interface IUnityProjectScanner
{
    Task<IReadOnlyList<UnityProject>> ScanAsync(
        IEnumerable<string> rootPaths,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
