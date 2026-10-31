namespace UnityProjectGallery.Core.Scanning;

/// <summary>扫描阶段。</summary>
public enum ScanPhase
{
    Starting,
    Scanning,
    Completed
}

/// <summary>扫描器向界面报告的进度快照。</summary>
public sealed record ScanProgress(
    ScanPhase Phase,
    int RootIndex,
    int RootCount,
    int ProjectsFound,
    string? CurrentDirectory,
    string? Message = null);
