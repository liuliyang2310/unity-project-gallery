namespace UnityProjectGallery.Models;

/// <summary>
/// 随工程目录一起保存的用户编辑数据，写入 `_ProjectPreview/project-gallery.json`。
/// 只包含用户维护的内容与相对路径，不包含缓存或本机绝对路径。
/// </summary>
public sealed class ProjectMetadataDocument
{
    /// <summary>当前文档结构版本。</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>最后一次用户编辑的 UTC 时间，用于与 SQLite 快照比较新旧。</summary>
    public DateTimeOffset SavedAt { get; init; }

    public string Description { get; init; } = string.Empty;

    public string[] Tags { get; init; } = Array.Empty<string>();

    public ProjectFeature[] Features { get; init; } = Array.Empty<ProjectFeature>();

    public ProjectMediaMetadata[] Media { get; init; } = Array.Empty<ProjectMediaMetadata>();

    /// <summary>封面素材相对 `_ProjectPreview` 的路径。</summary>
    public string? CoverRelativePath { get; init; }

    public bool IsFavorite { get; init; }

    public DateTimeOffset? LastOpenedTime { get; init; }
}

/// <summary>单条素材的用户编辑状态。</summary>
public sealed class ProjectMediaMetadata
{
    /// <summary>相对于 `_ProjectPreview` 的路径。</summary>
    public string RelativePath { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsIncluded { get; init; } = true;

    public string? FeatureId { get; init; }
}
