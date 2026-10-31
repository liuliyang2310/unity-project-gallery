using System.IO;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>Assets 列表中的一条可预览资源。</summary>
public sealed class AssetPreviewItemViewModel
{
    public AssetPreviewItemViewModel(AssetPreviewInfo asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        Asset = asset;
    }

    public AssetPreviewInfo Asset { get; }

    public string FilePath => Asset.FilePath;

    public string FileName => Asset.FileName;

    public string RelativePath => Asset.RelativePath;

    public AssetPreviewKind Kind => Asset.Kind;

    public string KindText => Kind switch
    {
        AssetPreviewKind.Model => "模型",
        AssetPreviewKind.Image => "图片",
        AssetPreviewKind.Audio => "音频",
        _ => "资源"
    };

    public string KindGlyph => Kind switch
    {
        AssetPreviewKind.Model => "\uE8A9",
        AssetPreviewKind.Image => "\uE91B",
        AssetPreviewKind.Audio => "\uE8D6",
        _ => "\uE8A9"
    };

    public string ExtensionText =>
        Path.GetExtension(Asset.FileName).TrimStart('.').ToUpperInvariant();

    public string SizeText => FormatSize(Asset.SizeBytes);

    public string ModifiedText => Asset.LastModifiedTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public bool Matches(string keyword)
    {
        return Asset.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               Asset.RelativePath.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        return bytes < 1024L * 1024 * 1024
            ? $"{bytes / (1024.0 * 1024):F1} MB"
            : $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
