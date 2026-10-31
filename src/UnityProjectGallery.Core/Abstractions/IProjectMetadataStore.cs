using UnityProjectGallery.Models;

namespace UnityProjectGallery.Core.Abstractions;

/// <summary>把用户编辑数据同步到工程目录内的元数据文件。</summary>
public interface IProjectMetadataStore
{
    Task<ProjectMetadataDocument?> ReadAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        string projectPath,
        ProjectMetadataDocument document,
        CancellationToken cancellationToken = default);
}
