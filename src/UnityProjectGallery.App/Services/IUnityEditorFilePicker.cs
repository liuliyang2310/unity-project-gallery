namespace UnityProjectGallery.App.Services;

/// <summary>选择 Unity.exe 的交互入口，便于在设置页隔离测试。</summary>
public interface IUnityEditorFilePicker
{
    string? PickExecutable();
}
