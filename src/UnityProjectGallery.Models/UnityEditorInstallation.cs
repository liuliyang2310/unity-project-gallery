namespace UnityProjectGallery.Models;

/// <summary>本机发现或用户指定的一款 Unity Editor。</summary>
public sealed class UnityEditorInstallation
{
    public required string Version { get; init; }

    public required string ExecutablePath { get; init; }
}
