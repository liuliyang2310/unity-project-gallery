using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityProjectGallery.Infrastructure.Common;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Infrastructure.Media;

/// <summary>
/// 缩略图缓存目录。缩略图只保存在软件本地缓存中，不写入原始 Unity 工程。
/// </summary>
public sealed class MediaThumbnailCache
{
    public MediaThumbnailCache(string? cacheDirectory = null)
    {
        RootDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityProjectGallery",
            "Cache",
            "Thumbnails");
    }

    public string RootDirectory { get; }

    public string GetThumbnailPath(string projectPath, string mediaPath, ProjectMediaKind kind)
    {
        var cacheKey = string.Join(
            "\n",
            ProjectPathHelper.Normalize(projectPath),
            ProjectPathHelper.Normalize(mediaPath),
            kind.ToString());

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)))
            .ToLowerInvariant()[..32];
        var folder = Path.Combine(RootDirectory, kind == ProjectMediaKind.Image ? "Images" : "Videos");
        Directory.CreateDirectory(folder);

        return Path.Combine(folder, hash + ".png");
    }
}
