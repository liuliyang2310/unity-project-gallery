namespace UnityProjectGallery.App.Services;

/// <summary>打开图片大图或视频播放窗口。</summary>
public interface IMediaViewerService
{
    void ShowImage(string filePath, string title);

    void ShowVideo(string filePath, string title);
}
