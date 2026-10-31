namespace UnityProjectGallery.Models;

/// <summary>
/// 一个本地 Unity 工程的元数据。
/// 扫描阶段填充版本与依赖信息，版本与素材等字段会在后续阶段继续扩展。
/// </summary>
public sealed class UnityProject
{
    /// <summary>工程稳定标识，由规范化工程路径派生。</summary>
    public required string Id { get; init; }

    /// <summary>工程显示名称，默认取工程目录名。</summary>
    public required string Name { get; init; }

    /// <summary>工程根目录绝对路径。</summary>
    public required string ProjectPath { get; init; }

    /// <summary>ProjectSettings/ProjectVersion.txt 中记录的 Unity 版本。</summary>
    public required string UnityVersion { get; init; }

    /// <summary>项目简介，可由用户手动编辑。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>项目标签。</summary>
    public string[] Tags { get; init; } = Array.Empty<string>();

    /// <summary>Packages/manifest.json 中声明的依赖包。</summary>
    public UnityPackageReference[] Packages { get; init; } = Array.Empty<UnityPackageReference>();

    /// <summary>项目功能模块。</summary>
    public ProjectFeature[] Features { get; init; } = Array.Empty<ProjectFeature>();

    /// <summary>`_ProjectPreview` 素材索引及用户展示状态。</summary>
    public ProjectMediaAsset[] Media { get; init; } = Array.Empty<ProjectMediaAsset>();

    /// <summary>手动或自动选定的封面素材路径。</summary>
    public string? CoverMediaPath { get; init; }

    /// <summary>是否收藏。</summary>
    public bool IsFavorite { get; init; }

    /// <summary>最近一次通过本软件启动工程的时间。</summary>
    public DateTimeOffset? LastOpenedTime { get; init; }

    /// <summary>最后一次用户编辑的时间，用于与工程目录内的元数据文件比较新旧。</summary>
    public DateTimeOffset? UserDataUpdatedAt { get; init; }

    /// <summary>工程目录最近一次被修改的时间。</summary>
    public DateTimeOffset? LastModifiedTime { get; init; }

    /// <summary>最近一次扫描时间。</summary>
    public DateTimeOffset? LastScanTime { get; init; }

    /// <summary>已索引的截图数量。</summary>
    public int ScreenshotCount { get; init; }

    /// <summary>已索引的视频数量。</summary>
    public int VideoCount { get; init; }

    /// <summary>已录入的功能说明数量。</summary>
    public int FeatureCount { get; init; }
}
