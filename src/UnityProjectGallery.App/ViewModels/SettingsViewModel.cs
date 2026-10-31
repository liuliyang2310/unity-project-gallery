using System.Collections.ObjectModel;
using System.IO;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.Core.Editors;
using UnityProjectGallery.Core.Mvvm;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>
/// Unity Editor 管理页面：加载自动发现与手动添加的安装记录，
/// 维护默认启动版本，并允许删除失效路径。
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly IUnityEditorSettingsStore _settingsStore;
    private readonly IUnityEditorLocator _editorLocator;
    private readonly IUnityEditorVersionReader _versionReader;
    private readonly IUnityEditorFilePicker _filePicker;
    private readonly Action _backToGallery;
    private readonly RelayCommand _addEditorCommand;
    private readonly RelayCommand _refreshCommand;
    private readonly RelayCommand _cleanupMissingCommand;

    private List<UnityEditorRecord> _manualRecords = new();
    private IReadOnlyList<UnityEditorInstallation> _discoveredEditors =
        Array.Empty<UnityEditorInstallation>();
    private string? _defaultVersion;
    private bool _isLoading;
    private string _statusMessage = "正在检测本机 Unity Editor…";

    public SettingsViewModel(
        IUnityEditorSettingsStore settingsStore,
        IUnityEditorLocator editorLocator,
        IUnityEditorVersionReader versionReader,
        IUnityEditorFilePicker filePicker,
        Action backToGallery)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(editorLocator);
        ArgumentNullException.ThrowIfNull(versionReader);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(backToGallery);

        _settingsStore = settingsStore;
        _editorLocator = editorLocator;
        _versionReader = versionReader;
        _filePicker = filePicker;
        _backToGallery = backToGallery;

        _addEditorCommand = new RelayCommand(
            () => _ = AddEditorAsync(),
            () => !IsLoading);
        _refreshCommand = new RelayCommand(
            () => _ = RefreshAsync(),
            () => !IsLoading);
        _cleanupMissingCommand = new RelayCommand(
            () => _ = CleanupMissingAsync(),
            () => !IsLoading && CanCleanupMissing);
        BackCommand = new RelayCommand(() => _backToGallery());
    }

    public ObservableCollection<UnityEditorEntryViewModel> Editors { get; } = new();

    public RelayCommand AddEditorCommand => _addEditorCommand;

    public RelayCommand RefreshCommand => _refreshCommand;

    public RelayCommand CleanupMissingCommand => _cleanupMissingCommand;

    public RelayCommand BackCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                _addEditorCommand.RaiseCanExecuteChanged();
                _refreshCommand.RaiseCanExecuteChanged();
                _cleanupMissingCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string AppVersionText => $"版本 {AppLog.AppVersion}";

    public bool IsEmpty => Editors.Count == 0;

    public bool CanCleanupMissing => _manualRecords.Any(record =>
        !File.Exists(record.ExecutablePath));

    public string DefaultVersionText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_defaultVersion))
            {
                return "未设置默认启动版本";
            }

            var matches = Editors
                .Where(editor => VersionEquals(editor.Version, _defaultVersion))
                .ToList();
            if (matches.Count == 0)
            {
                return $"默认启动版本：Unity {_defaultVersion}（未找到）";
            }

            if (!matches.Any(editor => editor.IsAvailable))
            {
                return $"默认启动版本：Unity {_defaultVersion}（路径失效）";
            }

            return $"默认启动版本：Unity {_defaultVersion}";
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadAsync(cancellationToken);
    }

    public void RefreshAvailability()
    {
        foreach (var editor in Editors)
        {
            editor.RefreshAvailability();
        }

        RaiseSummaryProperties();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await ReloadAsync(cancellationToken);
    }

    public async Task AddEditorAsync()
    {
        if (IsLoading)
        {
            return;
        }

        var selectedPath = _filePicker.PickExecutable();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var executablePath = Path.GetFullPath(selectedPath.Trim());
        if (!File.Exists(executablePath))
        {
            StatusMessage = $"所选文件不存在：{executablePath}";
            return;
        }

        if (!string.Equals(
                Path.GetFileName(executablePath),
                "Unity.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "请选择 Unity.exe 文件。";
            return;
        }

        if (_manualRecords.Any(record => PathsEqual(record.ExecutablePath, executablePath)) ||
            _discoveredEditors.Any(editor => PathsEqual(editor.ExecutablePath, executablePath)))
        {
            StatusMessage = "该 Unity Editor 路径已存在。";
            return;
        }

        var version = _versionReader.ReadVersion(executablePath);
        if (string.IsNullOrWhiteSpace(version))
        {
            StatusMessage = "无法从该文件读取 Unity 版本，请确认选择的是 Unity.exe。";
            return;
        }

        var record = new UnityEditorRecord
        {
            Version = version,
            ExecutablePath = executablePath,
            AddedAt = DateTimeOffset.UtcNow
        };

        _manualRecords.Add(record);
        try
        {
            await SaveSettingsAsync();
        }
        catch (Exception exception)
        {
            _manualRecords.Remove(record);
            StatusMessage = $"保存 Unity Editor 设置失败：{exception.Message}";
            return;
        }

        RebuildRows();
        StatusMessage = $"已添加 Unity {version}：{executablePath}";
    }

    public async Task ToggleDefaultVersionAsync(UnityEditorEntryViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        var previousDefault = _defaultVersion;
        _defaultVersion = editor.IsDefaultVersion ? null : editor.Version;

        try
        {
            await SaveSettingsAsync();
        }
        catch (Exception exception)
        {
            _defaultVersion = previousDefault;
            StatusMessage = $"保存默认启动版本失败：{exception.Message}";
            return;
        }

        ApplyDefaultFlags();
        StatusMessage = string.IsNullOrWhiteSpace(_defaultVersion)
            ? "已取消默认启动版本。"
            : $"默认启动版本已设为 Unity {_defaultVersion}。";
    }

    public async Task RemoveEditorAsync(UnityEditorEntryViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (!editor.IsManual)
        {
            return;
        }

        var previousRecords = _manualRecords.ToList();
        var previousDefault = _defaultVersion;

        _manualRecords.RemoveAll(record =>
            PathsEqual(record.ExecutablePath, editor.ExecutablePath));
        ClearDefaultIfUnavailable();

        try
        {
            await SaveSettingsAsync();
        }
        catch (Exception exception)
        {
            _manualRecords = previousRecords;
            _defaultVersion = previousDefault;
            StatusMessage = $"删除 Unity Editor 设置失败：{exception.Message}";
            RebuildRows();
            return;
        }

        RebuildRows();
        StatusMessage = $"已删除 Unity {editor.Version} 的路径记录。";
    }

    public async Task CleanupMissingAsync()
    {
        var missingRecords = _manualRecords
            .Where(record => !File.Exists(record.ExecutablePath))
            .ToList();
        if (missingRecords.Count == 0)
        {
            StatusMessage = "没有需要清理的失效路径记录。";
            return;
        }

        var previousRecords = _manualRecords.ToList();
        var previousDefault = _defaultVersion;

        _manualRecords.RemoveAll(record => !File.Exists(record.ExecutablePath));
        ClearDefaultIfUnavailable();

        try
        {
            await SaveSettingsAsync();
        }
        catch (Exception exception)
        {
            _manualRecords = previousRecords;
            _defaultVersion = previousDefault;
            StatusMessage = $"清理失效路径失败：{exception.Message}";
            RebuildRows();
            return;
        }

        RebuildRows();
        StatusMessage = $"已清理 {missingRecords.Count} 条失效路径记录。";
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        StatusMessage = "正在检测本机 Unity Editor…";
        try
        {
            var settings = await _settingsStore.LoadAsync(cancellationToken);
            _manualRecords = settings.Editors?
                .Where(record =>
                    !string.IsNullOrWhiteSpace(record.Version) &&
                    !string.IsNullOrWhiteSpace(record.ExecutablePath))
                .ToList() ?? new List<UnityEditorRecord>();
            _defaultVersion = NormalizeVersion(settings.DefaultVersion);

            string? discoveryError = null;
            try
            {
                _discoveredEditors = await _editorLocator.DiscoverAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _discoveredEditors = Array.Empty<UnityEditorInstallation>();
                discoveryError = $"自动检测 Unity Editor 失败：{exception.Message}";
            }

            RebuildRows();
            if (discoveryError is not null)
            {
                StatusMessage = discoveryError;
            }
            else
            {
                var missingCount = Editors.Count(editor => editor.IsMissing);
                StatusMessage = missingCount == 0
                    ? $"已加载 {Editors.Count} 个 Unity Editor。"
                    : $"已加载 {Editors.Count} 个 Unity Editor，其中 {missingCount} 个路径失效。";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RebuildRows()
    {
        var merged = new Dictionary<string, (string Version, bool IsManual)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var record in _manualRecords)
        {
            merged[record.ExecutablePath] = (record.Version, true);
        }

        foreach (var editor in _discoveredEditors)
        {
            if (!merged.ContainsKey(editor.ExecutablePath))
            {
                merged[editor.ExecutablePath] = (editor.Version, false);
            }
        }

        Editors.Clear();
        foreach (var pair in merged
                     .OrderByDescending(item => item.Value.Version, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            Editors.Add(new UnityEditorEntryViewModel(
                pair.Value.Version,
                pair.Key,
                pair.Value.IsManual,
                IsDefaultVersion(pair.Value.Version),
                item => _ = ToggleDefaultVersionAsync(item),
                item => _ = RemoveEditorAsync(item)));
        }

        RaiseSummaryProperties();
    }

    private void ApplyDefaultFlags()
    {
        foreach (var editor in Editors)
        {
            editor.SetDefaultVersion(IsDefaultVersion(editor.Version));
        }

        OnPropertyChanged(nameof(DefaultVersionText));
    }

    private void ClearDefaultIfUnavailable()
    {
        if (string.IsNullOrWhiteSpace(_defaultVersion))
        {
            return;
        }

        var available = _manualRecords.Any(record =>
                VersionEquals(record.Version, _defaultVersion)) ||
            _discoveredEditors.Any(editor =>
                VersionEquals(editor.Version, _defaultVersion));

        if (!available)
        {
            _defaultVersion = null;
        }
    }

    private bool IsDefaultVersion(string version)
    {
        return !string.IsNullOrWhiteSpace(_defaultVersion) &&
               VersionEquals(version, _defaultVersion);
    }

    private static bool VersionEquals(string left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
    }

    private async Task SaveSettingsAsync()
    {
        await _settingsStore.SaveAsync(new UnityEditorSettingsDocument
        {
            DefaultVersion = _defaultVersion,
            Editors = _manualRecords
                .Select(record => new UnityEditorRecord
                {
                    Version = record.Version,
                    ExecutablePath = record.ExecutablePath,
                    AddedAt = record.AddedAt
                })
                .ToArray()
        });
    }

    private void RaiseSummaryProperties()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanCleanupMissing));
        OnPropertyChanged(nameof(DefaultVersionText));
        _cleanupMissingCommand.RaiseCanExecuteChanged();
    }
}
