namespace UnityProjectGallery.App.Services;

/// <summary>在系统资源管理器中打开指定文件夹。</summary>
public interface IProjectFolderRevealService
{
    bool Reveal(string folderPath);
}
