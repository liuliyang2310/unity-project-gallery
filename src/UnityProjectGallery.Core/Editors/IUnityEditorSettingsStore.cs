using UnityProjectGallery.Models;

namespace UnityProjectGallery.Core.Editors;

/// <summary>读取并保存用户管理的 Unity Editor 设置。</summary>
public interface IUnityEditorSettingsStore
{
    Task<UnityEditorSettingsDocument> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        UnityEditorSettingsDocument settings,
        CancellationToken cancellationToken = default);
}

/// <summary>从 Unity.exe 自动读取 Unity 版本。</summary>
public interface IUnityEditorVersionReader
{
    string? ReadVersion(string executablePath);
}
