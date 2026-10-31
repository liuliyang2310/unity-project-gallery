namespace UnityProjectGallery.Models;

/// <summary>用户管理的 Unity Editor 安装记录与默认启动版本。</summary>
public sealed class UnityEditorSettingsDocument
{
    public int SchemaVersion { get; init; } = 1;

    /// <summary>没有匹配工程版本时尝试使用的默认 Unity 版本。</summary>
    public string? DefaultVersion { get; init; }

    public UnityEditorRecord[] Editors { get; init; } = Array.Empty<UnityEditorRecord>();
}

/// <summary>一条用户手动添加的 Unity Editor 安装记录。</summary>
public sealed class UnityEditorRecord
{
    /// <summary>从 Unity.exe 自动读取的版本号。</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Unity.exe 的完整路径。</summary>
    public string ExecutablePath { get; init; } = string.Empty;

    public DateTimeOffset AddedAt { get; init; }
}
