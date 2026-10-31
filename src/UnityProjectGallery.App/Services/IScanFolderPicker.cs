namespace UnityProjectGallery.App.Services;

/// <summary>扫描根目录选择服务，便于在 ViewModel 中隔离 WPF 对话框。</summary>
public interface IScanFolderPicker
{
    /// <summary>弹出目录选择框，用户取消时返回 <see langword="null"/>。</summary>
    string? PickFolder();
}
