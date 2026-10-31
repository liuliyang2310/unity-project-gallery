using UnityProjectGallery.Models;

namespace UnityProjectGallery.Core.Media;

/// <summary>单个工程的展示素材扫描结果。</summary>
public sealed record ProjectMediaScanResult(
    string ProjectPath,
    IReadOnlyList<ProjectMediaAsset> Media,
    IReadOnlyList<string> Warnings,
    bool IsFFmpegAvailable);
