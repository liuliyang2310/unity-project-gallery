using UnityProjectGallery.Models;

namespace UnityProjectGallery.Core.Editors;

/// <summary>发现本机已安装的 Unity Editor。</summary>
public interface IUnityEditorLocator
{
    Task<IReadOnlyList<UnityEditorInstallation>> DiscoverAsync(
        CancellationToken cancellationToken = default);
}
