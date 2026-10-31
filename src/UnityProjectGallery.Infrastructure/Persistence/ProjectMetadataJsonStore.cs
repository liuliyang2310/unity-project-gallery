using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityProjectGallery.Core.Abstractions;
using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Infrastructure.Persistence;

/// <summary>
/// 将用户编辑数据写入工程 `_ProjectPreview/project-gallery.json`。
/// 使用临时文件替换的方式避免写入中断产生半截 JSON。
/// </summary>
public sealed class ProjectMetadataJsonStore : IProjectMetadataStore
{
    public const string FileName = "project-gallery.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public static string GetMetadataPath(string projectPath)
    {
        return Path.Combine(
            projectPath,
            ProjectMediaConventions.PreviewDirectoryName,
            FileName);
    }

    public async Task<ProjectMetadataDocument?> ReadAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        var metadataPath = GetMetadataPath(projectPath);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(metadataPath, cancellationToken);
            return JsonSerializer.Deserialize<ProjectMetadataDocument>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // 元数据文件损坏时忽略，继续使用 SQLite 快照。
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task WriteAsync(
        string projectPath,
        ProjectMetadataDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException(
                $"工程目录不存在，无法写入元数据：{projectPath}");
        }

        await _writeGate.WaitAsync(cancellationToken);
        string? temporaryPath = null;
        try
        {
            var previewDirectory = Path.Combine(
                projectPath,
                ProjectMediaConventions.PreviewDirectoryName);
            Directory.CreateDirectory(previewDirectory);

            var metadataPath = Path.Combine(previewDirectory, FileName);
            temporaryPath = metadataPath + ".tmp";
            var json = JsonSerializer.Serialize(document, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, Utf8NoBom, cancellationToken);
            File.Move(temporaryPath, metadataPath, overwrite: true);
            temporaryPath = null;
        }
        finally
        {
            _writeGate.Release();

            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
