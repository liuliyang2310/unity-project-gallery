namespace UnityProjectGallery.Models;

/// <summary>Assets 预览支持的资源类型。</summary>
public enum AssetPreviewKind
{
    Model,
    Image,
    Audio
}

/// <summary>`Assets` 目录中发现的一个可预览资源。</summary>
public sealed record AssetPreviewInfo(
    string FilePath,
    string RelativePath,
    string FileName,
    AssetPreviewKind Kind,
    long SizeBytes,
    DateTimeOffset LastModifiedTime);
