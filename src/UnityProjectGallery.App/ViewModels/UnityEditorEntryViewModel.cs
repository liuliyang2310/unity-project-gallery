using System.IO;
using UnityProjectGallery.Core.Mvvm;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>Unity Editor 管理表格中的一行。</summary>
public sealed class UnityEditorEntryViewModel : ObservableObject
{
    private readonly Action<UnityEditorEntryViewModel> _toggleDefault;
    private readonly Action<UnityEditorEntryViewModel> _remove;
    private bool _isAvailable;
    private bool _isDefaultVersion;

    public UnityEditorEntryViewModel(
        string version,
        string executablePath,
        bool isManual,
        bool isDefaultVersion,
        Action<UnityEditorEntryViewModel> toggleDefault,
        Action<UnityEditorEntryViewModel> remove)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(toggleDefault);
        ArgumentNullException.ThrowIfNull(remove);

        Version = version;
        ExecutablePath = executablePath;
        IsManual = isManual;
        _isDefaultVersion = isDefaultVersion;
        _isAvailable = File.Exists(executablePath);
        _toggleDefault = toggleDefault;
        _remove = remove;

        EditorPath = Path.GetDirectoryName(executablePath) ?? executablePath;
        ToggleDefaultCommand = new RelayCommand(() => _toggleDefault(this));
        RemoveCommand = new RelayCommand(() => _remove(this));
    }

    public string Version { get; }

    public string ExecutablePath { get; }

    /// <summary>表格中展示的安装目录。</summary>
    public string EditorPath { get; }

    public bool IsManual { get; }

    public string SourceText => IsManual ? "手动添加" : "自动发现";

    public bool CanRemove => IsManual;

    public bool IsAvailable
    {
        get => _isAvailable;
        private set
        {
            if (SetProperty(ref _isAvailable, value))
            {
                OnPropertyChanged(nameof(IsMissing));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsMissing => !IsAvailable;

    public string StatusText => IsAvailable ? "可用" : "文件缺失";

    public bool IsDefaultVersion
    {
        get => _isDefaultVersion;
        private set
        {
            if (SetProperty(ref _isDefaultVersion, value))
            {
                OnPropertyChanged(nameof(DefaultActionText));
            }
        }
    }

    public string DefaultActionText => IsDefaultVersion ? "取消默认" : "设为默认";

    public RelayCommand ToggleDefaultCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public void RefreshAvailability()
    {
        IsAvailable = File.Exists(ExecutablePath);
    }

    public void SetDefaultVersion(bool isDefault)
    {
        IsDefaultVersion = isDefault;
    }
}
