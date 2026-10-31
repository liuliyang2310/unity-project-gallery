using System.IO;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.Core.Abstractions;
using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Core.Mvvm;
using UnityProjectGallery.Core.Scanning;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>
/// 管理扫描源、扫描进度与取消操作。实际的目录遍历由 <see cref="IUnityProjectScanner"/> 在后台执行。
/// </summary>
public sealed class ScanViewModel : ObservableObject
{
    private readonly IUnityProjectScanner _scanner;
    private readonly IProjectMediaScanner _mediaScanner;
    private readonly IProjectRepository _repository;
    private readonly IScanFolderPicker _folderPicker;
    private readonly Func<IReadOnlyList<ScannedProjectBundle>, Task<string>> _applyResults;
    private readonly Action<string> _showStatus;
    private readonly List<string> _scanRoots = new();
    private readonly RelayCommand _addScanRootCommand;
    private readonly RelayCommand _rescanCommand;
    private readonly RelayCommand _cancelScanCommand;

    private CancellationTokenSource? _cancellation;
    private bool _isScanning;
    private string _scanMessage = "正在准备扫描…";
    private string _currentDirectory = string.Empty;
    private string _rootProgressText = string.Empty;
    private string _foundText = "已发现 0 个工程";
    private bool _scanningMedia;

    public ScanViewModel(
        IUnityProjectScanner scanner,
        IProjectMediaScanner mediaScanner,
        IProjectRepository repository,
        IScanFolderPicker folderPicker,
        Func<IReadOnlyList<ScannedProjectBundle>, Task<string>> applyResults,
        Action<string> showStatus)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(mediaScanner);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(folderPicker);
        ArgumentNullException.ThrowIfNull(applyResults);
        ArgumentNullException.ThrowIfNull(showStatus);

        _scanner = scanner;
        _mediaScanner = mediaScanner;
        _repository = repository;
        _folderPicker = folderPicker;
        _applyResults = applyResults;
        _showStatus = showStatus;

        _addScanRootCommand = new RelayCommand(
            () => _ = AddScanRootAsync(),
            () => !IsScanning);
        _rescanCommand = new RelayCommand(
            () => _ = ScanRootsAsync(_scanRoots.ToList()),
            () => !IsScanning && _scanRoots.Count > 0);
        _cancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
    }

    public RelayCommand AddScanRootCommand => _addScanRootCommand;

    public RelayCommand RescanCommand => _rescanCommand;

    public RelayCommand CancelScanCommand => _cancelScanCommand;

    public int RootCount => _scanRoots.Count;

    public string ScanRootSummary => RootCount == 0
        ? "尚未添加扫描目录"
        : $"扫描源 {RootCount} 个";

    public string ScanRootsToolTip => RootCount == 0
        ? "尚未添加扫描目录"
        : string.Join(Environment.NewLine, _scanRoots);

    /// <summary>启动时恢复已保存的扫描源，不自动触发扫描。</summary>
    public void LoadRoots(IEnumerable<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        _scanRoots.Clear();
        foreach (var root in roots
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(NormalizeRoot)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _scanRoots.Add(root);
        }

        RaiseRootProperties();
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ScanMessage
    {
        get => _scanMessage;
        private set => SetProperty(ref _scanMessage, value);
    }

    public string CurrentDirectory
    {
        get => _currentDirectory;
        private set => SetProperty(ref _currentDirectory, value);
    }

    public string RootProgressText
    {
        get => _rootProgressText;
        private set => SetProperty(ref _rootProgressText, value);
    }

    public string FoundText
    {
        get => _foundText;
        private set => SetProperty(ref _foundText, value);
    }

    private async Task AddScanRootAsync()
    {
        if (IsScanning)
        {
            return;
        }

        try
        {
            var folder = _folderPicker.PickFolder();
            if (folder is null)
            {
                return;
            }

            var normalized = NormalizeRoot(folder);
            if (_scanRoots.Any(root =>
                    string.Equals(root, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                _showStatus("该目录已在扫描源中，无需重复添加。");
                return;
            }

            _scanRoots.Add(normalized);
            RaiseRootProperties();
            _showStatus($"已添加扫描目录：{normalized}");
            await _repository.SaveScanRootsAsync(_scanRoots.ToArray());

            // 仅扫描新加入的目录；需要全量更新时使用“重新扫描”。
            await ScanRootsAsync(new[] { normalized });
        }
        catch (OperationCanceledException)
        {
            _showStatus("扫描已取消。");
        }
        catch (Exception exception)
        {
            _showStatus($"无法添加扫描目录：{exception.Message}");
        }
    }

    private async Task ScanRootsAsync(IReadOnlyList<string> roots)
    {
        if (IsScanning || roots.Count == 0)
        {
            return;
        }

        IsScanning = true;
        _scanningMedia = false;
        _cancellation = new CancellationTokenSource();
        ScanMessage = "正在准备扫描…";
        CurrentDirectory = string.Empty;
        RootProgressText = string.Empty;
        FoundText = "已发现 0 个工程";
        _showStatus("正在扫描工程目录…");

        try
        {
            var progress = new Progress<ScanProgress>(OnProgressChanged);
            var scannedProjects = await _scanner.ScanAsync(
                roots,
                progress,
                _cancellation.Token);

            _cancellation.Token.ThrowIfCancellationRequested();

            var bundles = await ScanMediaAsync(scannedProjects, _cancellation.Token);
            _cancellation.Token.ThrowIfCancellationRequested();

            var summary = await _applyResults(bundles);
            _showStatus(summary);
        }
        catch (OperationCanceledException)
        {
            _showStatus("扫描已取消，工程列表保持不变。");
        }
        catch (Exception exception)
        {
            _showStatus($"扫描失败：{exception.Message}");
        }
        finally
        {
            _scanningMedia = false;
            IsScanning = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private async Task<IReadOnlyList<ScannedProjectBundle>> ScanMediaAsync(
        IReadOnlyList<UnityProject> projects,
        CancellationToken cancellationToken)
    {
        var bundles = new List<ScannedProjectBundle>(projects.Count);
        if (projects.Count == 0)
        {
            return bundles;
        }

        _scanningMedia = true;
        ScanMessage = "正在索引 _ProjectPreview 素材…";
        FoundText = "已索引 0 张图片 · 0 个视频";
        _showStatus("正在索引展示素材…");

        var imageCount = 0;
        var videoCount = 0;

        for (var index = 0; index < projects.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = projects[index];

            CurrentDirectory = Path.Combine(
                project.ProjectPath,
                ProjectMediaConventions.PreviewDirectoryName);
            RootProgressText = $"工程 {index + 1}/{projects.Count}";

            var mediaResult = await _mediaScanner.ScanAsync(project.ProjectPath, cancellationToken);
            imageCount += mediaResult.Media.Count(media => media.Kind == ProjectMediaKind.Image);
            videoCount += mediaResult.Media.Count(media => media.Kind == ProjectMediaKind.Video);
            FoundText = $"已索引 {imageCount} 张图片 · {videoCount} 个视频";

            bundles.Add(new ScannedProjectBundle(project, mediaResult));
        }

        _scanningMedia = false;
        return bundles;
    }

    private void OnProgressChanged(ScanProgress progress)
    {
        if (!IsScanning || _scanningMedia)
        {
            return;
        }

        ScanMessage = progress.Message ?? progress.Phase switch
        {
            ScanPhase.Starting => "正在准备扫描…",
            ScanPhase.Scanning => "正在扫描目录结构",
            ScanPhase.Completed => "扫描完成",
            _ => "正在扫描…"
        };
        CurrentDirectory = progress.CurrentDirectory ?? string.Empty;
        RootProgressText = progress.RootCount > 0
            ? $"目录 {progress.RootIndex}/{progress.RootCount}"
            : string.Empty;
        FoundText = $"已发现 {progress.ProjectsFound} 个工程";
    }

    private void CancelScan()
    {
        if (_cancellation is null)
        {
            return;
        }

        ScanMessage = "正在取消扫描…";
        _cancellation.Cancel();
    }

    private static string NormalizeRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (root is not null && fullPath.Length > root.Length)
        {
            fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return fullPath;
    }

    private void RaiseRootProperties()
    {
        OnPropertyChanged(nameof(RootCount));
        OnPropertyChanged(nameof(ScanRootSummary));
        OnPropertyChanged(nameof(ScanRootsToolTip));
        _rescanCommand.RaiseCanExecuteChanged();
    }

    private void RaiseCommandStates()
    {
        _addScanRootCommand.RaiseCanExecuteChanged();
        _rescanCommand.RaiseCanExecuteChanged();
        _cancelScanCommand.RaiseCanExecuteChanged();
    }
}
