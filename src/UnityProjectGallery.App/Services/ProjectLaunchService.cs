using System.IO;
using UnityProjectGallery.Core.Editors;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.Services;

/// <summary>
/// 优先使用精确版本的 Unity Editor；找不到时允许用户选择其他编辑器或通过 Unity Hub 打开。
/// 用户为版本指定的编辑器路径在当前运行期间保留。
/// </summary>
public sealed class ProjectLaunchService : IProjectLaunchService
{
    private readonly IUnityEditorLocator _editorLocator;
    private readonly IUnityLaunchPromptService _prompt;
    private readonly IExternalProcessLauncher _processLauncher;
    private readonly IUnityHubLocator _hubLocator;
    private readonly IUnityEditorSettingsStore _editorSettingsStore;
    private readonly Dictionary<string, string> _customEditorsByVersion =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<UnityEditorInstallation>? _discoveredEditors;

    public ProjectLaunchService(
        IUnityEditorLocator editorLocator,
        IUnityLaunchPromptService prompt,
        IExternalProcessLauncher processLauncher,
        IUnityHubLocator hubLocator,
        IUnityEditorSettingsStore editorSettingsStore)
    {
        ArgumentNullException.ThrowIfNull(editorLocator);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(processLauncher);
        ArgumentNullException.ThrowIfNull(hubLocator);
        ArgumentNullException.ThrowIfNull(editorSettingsStore);

        _editorLocator = editorLocator;
        _prompt = prompt;
        _processLauncher = processLauncher;
        _hubLocator = hubLocator;
        _editorSettingsStore = editorSettingsStore;
    }

    public async Task<ProjectLaunchResult> LaunchAsync(
        string projectName,
        string projectPath,
        string unityVersion,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(projectPath))
        {
            return new ProjectLaunchResult(false, $"工程路径不存在：{projectPath}");
        }

        try
        {
            var editor = await FindEditorAsync(unityVersion, cancellationToken);
            if (editor is not null)
            {
                var message = VersionEquals(editor.Version, unityVersion)
                    ? $"已使用 Unity {editor.Version} 启动工程。"
                    : $"未找到 Unity {unityVersion}，已使用默认版本 Unity {editor.Version} 启动工程。";
                return LaunchEditor(
                    projectName,
                    projectPath,
                    editor.ExecutablePath,
                    message);
            }

            var choice = _prompt.AskForMissingEditor(projectName, projectPath, unityVersion);
            return choice switch
            {
                UnityEditorMissingChoice.ChooseEditor => LaunchWithChosenEditor(
                    projectName,
                    projectPath,
                    unityVersion),
                UnityEditorMissingChoice.UseUnityHub => LaunchWithUnityHub(projectName, projectPath),
                _ => new ProjectLaunchResult(false, "已取消启动工程。")
            };
        }
        catch (OperationCanceledException)
        {
            return new ProjectLaunchResult(false, "已取消启动工程。");
        }
        catch (Exception exception)
        {
            return new ProjectLaunchResult(false, $"启动失败：{exception.Message}");
        }
    }

    private async Task<UnityEditorInstallation?> FindEditorAsync(
        string unityVersion,
        CancellationToken cancellationToken)
    {
        var settings = await _editorSettingsStore.LoadAsync(cancellationToken);

        // 用户手动管理且仍存在的同版本 Editor 优先。
        var managedEditor = settings.Editors.FirstOrDefault(record =>
            VersionEquals(record.Version, unityVersion) &&
            File.Exists(record.ExecutablePath));
        if (managedEditor is not null)
        {
            return new UnityEditorInstallation
            {
                Version = managedEditor.Version,
                ExecutablePath = managedEditor.ExecutablePath
            };
        }

        if (_customEditorsByVersion.TryGetValue(unityVersion, out var customEditor) &&
            File.Exists(customEditor))
        {
            return new UnityEditorInstallation
            {
                Version = unityVersion,
                ExecutablePath = customEditor
            };
        }

        _discoveredEditors ??= await _editorLocator.DiscoverAsync(cancellationToken);
        var exactEditor = _discoveredEditors.FirstOrDefault(editor =>
            VersionEquals(editor.Version, unityVersion) &&
            File.Exists(editor.ExecutablePath));
        if (exactEditor is not null)
        {
            return exactEditor;
        }

        // 没有精确版本时，尝试设置页指定的默认启动版本。
        var defaultVersion = settings.DefaultVersion;
        if (string.IsNullOrWhiteSpace(defaultVersion) ||
            VersionEquals(defaultVersion, unityVersion))
        {
            return null;
        }

        var managedDefault = settings.Editors.FirstOrDefault(record =>
            VersionEquals(record.Version, defaultVersion) &&
            File.Exists(record.ExecutablePath));
        if (managedDefault is not null)
        {
            return new UnityEditorInstallation
            {
                Version = managedDefault.Version,
                ExecutablePath = managedDefault.ExecutablePath
            };
        }

        return _discoveredEditors.FirstOrDefault(editor =>
            VersionEquals(editor.Version, defaultVersion) &&
            File.Exists(editor.ExecutablePath));
    }

    private static bool VersionEquals(string left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private ProjectLaunchResult LaunchWithChosenEditor(
        string projectName,
        string projectPath,
        string unityVersion)
    {
        var executablePath = _prompt.PickEditorExecutable();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new ProjectLaunchResult(false, "已取消选择 Unity Editor。");
        }

        if (!File.Exists(executablePath))
        {
            return new ProjectLaunchResult(false, $"所选 Unity Editor 不存在：{executablePath}");
        }

        _customEditorsByVersion[unityVersion] = executablePath;
        return LaunchEditor(
            projectName,
            projectPath,
            executablePath,
            "已使用所选 Unity Editor 启动工程。");
    }

    private ProjectLaunchResult LaunchWithUnityHub(string projectName, string projectPath)
    {
        var hubPath = _hubLocator.FindExecutable();
        if (string.IsNullOrWhiteSpace(hubPath) || !File.Exists(hubPath))
        {
            return new ProjectLaunchResult(
                false,
                "未找到 Unity Hub，请手动启动 Unity Hub 并打开该工程。");
        }

        var started = _processLauncher.Start(
            hubPath,
            new[] { "--", "--projectPath", projectPath },
            Path.GetDirectoryName(hubPath));

        return started
            ? new ProjectLaunchResult(
                true,
                "已通过 Unity Hub 打开工程。",
                DateTimeOffset.Now)
            : new ProjectLaunchResult(false, "无法启动 Unity Hub，请手动打开工程。");
    }

    private ProjectLaunchResult LaunchEditor(
        string projectName,
        string projectPath,
        string executablePath,
        string successMessage)
    {
        var started = _processLauncher.Start(
            executablePath,
            new[] { "-projectPath", projectPath },
            projectPath);

        return started
            ? new ProjectLaunchResult(true, successMessage, DateTimeOffset.Now)
            : new ProjectLaunchResult(false, $"无法启动「{projectName}」的 Unity Editor。");
    }
}
