namespace UnityProjectGallery.Models;

/// <summary>
/// 工程 `_ProjectPreview` 目录中的一条展示素材。
/// 记录原始文件路径与软件缓存中的缩略图路径，不复制或修改原始文件。
/// </summary>
public sealed class ProjectMediaAsset
{
    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public required ProjectMediaKind Kind { get; init; }

    /// <summary>相对 `_ProjectPreview` 的路径，用于显示与后续功能关联。</summary>
    public string RelativePath { get; init; } = string.Empty;

    public int? PixelWidth { get; init; }

    public int? PixelHeight { get; init; }

    public double? DurationSeconds { get; init; }

    /// <summary>软件缓存目录中的缩略图或视频封面路径。</summary>
    public string? ThumbnailPath { get; init; }

    /// <summary>用户为素材填写的说明。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>是否纳入展示索引。</summary>
    public bool IsIncluded { get; init; } = true;

    /// <summary>关联的功能 Id；未关联时为 null。</summary>
    public string? FeatureId { get; init; }
}
