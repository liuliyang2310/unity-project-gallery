namespace UnityProjectGallery.Models;

/// <summary>项目内的一个功能模块定义。</summary>
public sealed class ProjectFeature
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;
}
