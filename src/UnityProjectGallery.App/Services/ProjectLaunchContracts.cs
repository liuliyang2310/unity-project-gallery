namespace UnityProjectGallery.App.Services;

/// <summary>未找到对应版本 Unity Editor 时的用户选择。</summary>
public enum UnityEditorMissingChoice
{
    ChooseEditor,
    UseUnityHub,
    Cancel
}

/// <summary>启动工程时向用户询问补充信息。</summary>
public interface IUnityLaunchPromptService
{
    UnityEditorMissingChoice AskForMissingEditor(
        string projectName,
        string projectPath,
        string unityVersion);

    string? PickEditorExecutable();
}

/// <summary>启动外部进程，便于隔离测试。</summary>
public interface IExternalProcessLauncher
{
    bool Start(string executablePath, IReadOnlyList<string> arguments, string? workingDirectory);
}

/// <summary>定位 Unity Hub 可执行文件。</summary>
public interface IUnityHubLocator
{
    string? FindExecutable();
}

/// <summary>一次工程启动尝试的结果。</summary>
public sealed record ProjectLaunchResult(
    bool Launched,
    string Message,
    DateTimeOffset? OpenedAt = null);

/// <summary>根据工程 Unity 版本查找并启动 Editor。</summary>
public interface IProjectLaunchService
{
    Task<ProjectLaunchResult> LaunchAsync(
        string projectName,
        string projectPath,
        string unityVersion,
        CancellationToken cancellationToken = default);
}
