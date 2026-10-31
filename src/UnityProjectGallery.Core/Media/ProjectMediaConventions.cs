namespace UnityProjectGallery.Core.Media;

/// <summary>第三阶段素材扫描约定：仅访问工程根目录下的 `_ProjectPreview`。</summary>
public static class ProjectMediaConventions
{
    public const string PreviewDirectoryName = "_ProjectPreview";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".webm"
    };

    public static bool IsSupportedImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    public static bool IsSupportedVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path));
}
