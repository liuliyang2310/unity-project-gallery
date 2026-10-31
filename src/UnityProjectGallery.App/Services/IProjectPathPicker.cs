namespace UnityProjectGallery.App.Services;

/// <summary>选择移动后的 Unity 工程根目录。</summary>
public interface IProjectPathPicker
{
    string? PickProjectDirectory();
}
