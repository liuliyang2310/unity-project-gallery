using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityProjectGallery.Core.Editors;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.Infrastructure.Editors;

/// <summary>
/// 将手动添加的 Unity Editor 与默认启动版本保存到
/// `%LOCALAPPDATA%\UnityProjectGallery\unity-editors.json`。
/// </summary>
public sealed class JsonUnityEditorSettingsStore : IUnityEditorSettingsStore
{
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

    private readonly string _settingsPath;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public JsonUnityEditorSettingsStore(string? settingsPath = null)
    {
        _settingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? GetDefaultSettingsPath()
            : settingsPath;
    }

    public string SettingsPath => _settingsPath;

    public async Task<UnityEditorSettingsDocument> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new UnityEditorSettingsDocument();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
            var settings = JsonSerializer.Deserialize<UnityEditorSettingsDocument>(
                json,
                JsonOptions);
            return settings ?? new UnityEditorSettingsDocument();
        }
        catch (JsonException)
        {
            return new UnityEditorSettingsDocument();
        }
        catch (IOException)
        {
            return new UnityEditorSettingsDocument();
        }
        catch (UnauthorizedAccessException)
        {
            return new UnityEditorSettingsDocument();
        }
    }

    public async Task SaveAsync(
        UnityEditorSettingsDocument settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _writeGate.WaitAsync(cancellationToken);
        string? temporaryPath = null;
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            temporaryPath = _settingsPath + ".tmp";
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(temporaryPath, json, Utf8NoBom, cancellationToken);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
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

    private static string GetDefaultSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityProjectGallery",
            "unity-editors.json");
    }
}
