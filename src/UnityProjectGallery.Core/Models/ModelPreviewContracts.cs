using UnityProjectGallery.Models;

namespace UnityProjectGallery.Core.Models;

/// <summary>按需枚举 Unity 工程 Assets 中可预览的模型、图片与音频文件。</summary>
public interface IAssetsPreviewScanner
{
    Task<IReadOnlyList<AssetPreviewInfo>> ScanAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}

/// <summary>加载静态模型并转换为与 UI 无关的场景。</summary>
public interface IModelPreviewLoader
{
    Task<ModelPreviewResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

/// <summary>模型预览支持的扩展名。</summary>
public static class ModelAssetConventions
{
    public static readonly string[] SupportedExtensions =
    {
        ".fbx", ".obj", ".gltf", ".glb"
    };

    public static bool IsSupported(string filePath)
    {
        return SupportedExtensions.Any(extension =>
            filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Assets 预览支持的图片与音频扩展名。</summary>
public static class AssetPreviewConventions
{
    public static readonly string[] ImageExtensions =
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"
    };

    public static readonly string[] AudioExtensions =
    {
        ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".m4a"
    };

    public static bool IsSupportedImage(string filePath)
    {
        return ImageExtensions.Any(extension =>
            filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSupportedAudio(string filePath)
    {
        return AudioExtensions.Any(extension =>
            filePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryGetKind(string filePath, out AssetPreviewKind kind)
    {
        if (ModelAssetConventions.IsSupported(filePath))
        {
            kind = AssetPreviewKind.Model;
            return true;
        }

        if (IsSupportedImage(filePath))
        {
            kind = AssetPreviewKind.Image;
            return true;
        }

        if (IsSupportedAudio(filePath))
        {
            kind = AssetPreviewKind.Audio;
            return true;
        }

        kind = default;
        return false;
    }
}
