using UnityProjectGallery.Models;

namespace UnityProjectGallery.Core.Abstractions;

/// <summary>工程数据的持久化入口。</summary>
public interface IProjectRepository
{
    Task<IReadOnlyList<UnityProject>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>保存工程完整快照，包括素材索引、标签、功能与用户展示状态。</summary>
    Task SaveProjectAsync(UnityProject project, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetScanRootsAsync(CancellationToken cancellationToken = default);

    Task SaveScanRootsAsync(
        IReadOnlyList<string> scanRoots,
        CancellationToken cancellationToken = default);
}
