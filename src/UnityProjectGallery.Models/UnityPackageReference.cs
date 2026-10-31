namespace UnityProjectGallery.Models;

/// <summary>来自 Packages/manifest.json 的一条依赖声明。</summary>
public sealed class UnityPackageReference
{
    public required string Name { get; init; }

    public required string Version { get; init; }
}
