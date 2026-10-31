using System.Collections.ObjectModel;
using System.IO;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.Core.Abstractions;
using UnityProjectGallery.Core.Editors;
using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Core.Mvvm;
using UnityProjectGallery.Core.Scanning;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>
/// 应用壳层 ViewModel：负责导航、页面切换、扫描结果合并与全局状态提示。
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IProjectRepository _repository;
    private readonly IUnityProjectScanner _scanner;
    private readonly IProjectMediaScanner _mediaScanner;
    private readonly IScanFolderPicker _folderPicker;
    private readonly IMediaViewerService _mediaViewer;
    private readonly IProjectLaunchService _projectLaunchService;
    private readonly IProjectPathPicker _projectPathPicker;
    private readonly IProjectMetadataStore _metadataStore;
    private readonly IUnityEditorSettingsStore _editorSettingsStore;
    private readonly IUnityEditorLocator _editorLocator;
    private readonly IUnityEditorVersionReader _editorVersionReader;
    private readonly IUnityEditorFilePicker _editorFilePicker;
    private readonly IProjectFolderRevealService _folderRevealService;
    private readonly IAssetPreviewService _assetPreviewService;
    private readonly object _pendingSaveGate = new();
    private readonly HashSet<Task> _pendingSaves = new();
    private object? _currentPage;
    private string _statusMessage = "正在准备项目库…";
    private bool _isSettingsPageActive;
    private NavigationItemViewModel? _navigationBeforeSettings;

    public MainViewModel(
        IProjectRepository repository,
        IUnityProjectScanner scanner,
        IProjectMediaScanner mediaScanner,
        IScanFolderPicker folderPicker,
        IMediaViewerService mediaViewer,
        IProjectLaunchService projectLaunchService,
        IProjectPathPicker projectPathPicker,
        IProjectMetadataStore metadataStore,
        IUnityEditorSettingsStore editorSettingsStore,
        IUnityEditorLocator editorLocator,
        IUnityEditorVersionReader editorVersionReader,
        IUnityEditorFilePicker editorFilePicker,
        IProjectFolderRevealService folderRevealService,
        IAssetPreviewService assetPreviewService)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(mediaScanner);
        ArgumentNullException.ThrowIfNull(folderPicker);
        ArgumentNullException.ThrowIfNull(mediaViewer);
        ArgumentNullException.ThrowIfNull(projectLaunchService);
        ArgumentNullException.ThrowIfNull(projectPathPicker);
        ArgumentNullException.ThrowIfNull(metadataStore);
        ArgumentNullException.ThrowIfNull(editorSettingsStore);
        ArgumentNullException.ThrowIfNull(editorLocator);
        ArgumentNullException.ThrowIfNull(editorVersionReader);
        ArgumentNullException.ThrowIfNull(editorFilePicker);
        ArgumentNullException.ThrowIfNull(folderRevealService);
        ArgumentNullException.ThrowIfNull(assetPreviewService);

        _repository = repository;
        _scanner = scanner;
        _mediaScanner = mediaScanner;
        _folderPicker = folderPicker;
        _mediaViewer = mediaViewer;
        _projectLaunchService = projectLaunchService;
        _projectPathPicker = projectPathPicker;
        _metadataStore = metadataStore;
        _editorSettingsStore = editorSettingsStore;
        _editorLocator = editorLocator;
        _editorVersionReader = editorVersionReader;
        _editorFilePicker = editorFilePicker;
        _folderRevealService = folderRevealService;
        _assetPreviewService = assetPreviewService;

        OpenSettingsCommand = new RelayCommand(() => _ = OpenSettingsAsync());
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; } = new();

    public GalleryViewModel? Gallery { get; private set; }

    public SettingsViewModel? Settings { get; private set; }

    public RelayCommand OpenSettingsCommand { get; }

    public bool IsSettingsPageActive
    {
        get => _isSettingsPageActive;
        private set => SetProperty(ref _isSettingsPageActive, value);
    }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var projectModels = await _repository.GetAllAsync(cancellationToken);
        var scanRoots = await _repository.GetScanRootsAsync(cancellationToken);

        var metadataByPath = new Dictionary<string, ProjectMetadataDocument>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var model in projectModels)
        {
            var metadata = await _metadataStore.ReadAsync(model.ProjectPath, cancellationToken);
            if (metadata is not null)
            {
                metadataByPath[NormalizePath(model.ProjectPath)] = metadata;
            }
        }

        var projects = projectModels
            .Select(model => new ProjectItemViewModel(
                model,
                OpenProjectDetail,
                OpenMediaInViewer,
                RequestProjectSave,
                metadataByPath.GetValueOrDefault(NormalizePath(model.ProjectPath))))
            .ToList();

        Gallery = new GalleryViewModel(projects);
        Gallery.ProjectsChanged += OnGalleryProjectsChanged;
        Gallery.Scan = new ScanViewModel(
            _scanner,
            _mediaScanner,
            _repository,
            _folderPicker,
            ApplyScanResultsAsync,
            ShowStatus);
        Gallery.Scan.LoadRoots(scanRoots);
        OnPropertyChanged(nameof(Gallery));

        BuildNavigation();
        RefreshNavigationCounts();

        CurrentPage = Gallery;
        var missingPathCount = projects.Count(project => project.IsProjectPathMissing);
        StatusMessage = projects.Count == 0
            ? "就绪 · 添加扫描目录以开始只读扫描"
            : missingPathCount == 0
                ? $"就绪 · 已从本地数据库加载 {projects.Count} 个工程"
                : $"就绪 · 已加载 {projects.Count} 个工程，其中 {missingPathCount} 个路径失效";
    }

    private void BuildNavigation()
    {
        var all = new NavigationItemViewModel("全部工程", "\uE8A9", FilterMode.All, SelectNavigation);
        var recent = new NavigationItemViewModel("最近打开", "\uE81C", FilterMode.RecentOpened, SelectNavigation);
        var modified = new NavigationItemViewModel("最近修改", "\uE787", FilterMode.RecentlyModified, SelectNavigation);
        var favorites = new NavigationItemViewModel("收藏项目", "\uE734", FilterMode.Favorites, SelectNavigation);
        all.IsActive = true;

        NavigationItems.Clear();
        NavigationItems.Add(all);
        NavigationItems.Add(recent);
        NavigationItems.Add(modified);
        NavigationItems.Add(favorites);
    }

    private void SelectNavigation(NavigationItemViewModel item)
    {
        if (Gallery is null)
        {
            return;
        }

        IsSettingsPageActive = false;
        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsActive = ReferenceEquals(navigationItem, item);
        }

        Gallery.FilterMode = item.FilterMode;
        CurrentPage = Gallery;
    }

    private async Task OpenSettingsAsync()
    {
        if (Settings is null)
        {
            Settings = new SettingsViewModel(
                _editorSettingsStore,
                _editorLocator,
                _editorVersionReader,
                _editorFilePicker,
                GoBackToGallery);
            await Settings.InitializeAsync();
        }
        else
        {
            Settings.RefreshAvailability();
        }

        _navigationBeforeSettings = NavigationItems.FirstOrDefault(item => item.IsActive);
        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsActive = false;
        }

        IsSettingsPageActive = true;
        OnPropertyChanged(nameof(IsSettingsPageActive));
        CurrentPage = Settings;
        ShowStatus("Unity Editor 管理");
    }

    private void OnGalleryProjectsChanged(object? sender, EventArgs e)
    {
        RefreshNavigationCounts();
    }

    private void RefreshNavigationCounts()
    {
        if (Gallery is null)
        {
            return;
        }

        var projects = Gallery.AllProjects;
        var counts = new Dictionary<FilterMode, int>
        {
            [FilterMode.All] = projects.Count,
            [FilterMode.RecentOpened] = projects.Count(project => project.LastOpenedTime is not null),
            [FilterMode.RecentlyModified] = projects.Count(project => project.LastModifiedTime is not null),
            [FilterMode.Favorites] = projects.Count(project => project.IsFavorite)
        };

        foreach (var item in NavigationItems)
        {
            item.Count = counts[item.FilterMode];
        }
    }

    /// <summary>把一次扫描结果合并到当前列表，并返回适合状态栏显示的摘要。</summary>
    private async Task<string> ApplyScanResultsAsync(
        IReadOnlyList<ScannedProjectBundle> scannedBundles)
    {
        if (Gallery is null)
        {
            return "扫描完成。";
        }

        // 新工程优先读取工程目录内的元数据文件，使工程移动或数据库重建后也能恢复用户编辑。
        var existingPaths = Gallery.AllProjects
            .Select(project => NormalizePath(project.ProjectPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var metadataByPath = new Dictionary<string, ProjectMetadataDocument>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var bundle in scannedBundles)
        {
            var path = NormalizePath(bundle.Project.ProjectPath);
            if (existingPaths.Contains(path))
            {
                continue;
            }

            var metadata = await _metadataStore.ReadAsync(bundle.Project.ProjectPath);
            if (metadata is not null)
            {
                metadataByPath[path] = metadata;
            }
        }

        var merge = Gallery.MergeProjects(
            scannedBundles,
            model =>
            {
                metadataByPath.TryGetValue(NormalizePath(model.ProjectPath), out var metadata);
                return new ProjectItemViewModel(
                    model,
                    OpenProjectDetail,
                    OpenMediaInViewer,
                    RequestProjectSave,
                    metadata);
            });

        if (scannedBundles.Count == 0)
        {
            return "扫描完成，未发现 Unity 工程。";
        }

        var imageCount = scannedBundles.Sum(bundle =>
            bundle.MediaResult.Media.Count(media => media.Kind == ProjectMediaKind.Image));
        var videoCount = scannedBundles.Sum(bundle =>
            bundle.MediaResult.Media.Count(media => media.Kind == ProjectMediaKind.Video));

        return $"扫描完成：新增 {merge.Added} 个，更新 {merge.Updated} 个工程；索引 {imageCount} 张图片、{videoCount} 个视频。";
    }

    private void OpenProjectDetail(ProjectItemViewModel project)
    {
        project.RefreshPathAvailability();
        CurrentPage = new ProjectDetailViewModel(
            project,
            GoBackToGallery,
            ShowStatus,
            _projectLaunchService,
            _projectPathPicker,
            _folderRevealService,
            _assetPreviewService);
        ShowStatus($"正在查看「{project.Name}」");
    }

    /// <summary>
    /// 所有工程状态变更都在后台写入 SQLite；用户编辑额外同步到工程目录内的 JSON。
    /// </summary>
    private void RequestProjectSave(ProjectItemViewModel project, bool syncMetadata)
    {
        var saveTask = PersistProjectAsync(project, syncMetadata);
        lock (_pendingSaveGate)
        {
            _pendingSaves.Add(saveTask);
        }

        _ = saveTask.ContinueWith(
            completed =>
            {
                lock (_pendingSaveGate)
                {
                    _pendingSaves.Remove(completed);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>关闭窗口前等待已排队的保存任务完成，避免快速退出丢失数据。</summary>
    public async Task FlushPendingSavesAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (_pendingSaveGate)
            {
                pending = _pendingSaves.ToArray();
            }

            if (pending.Length == 0)
            {
                return;
            }

            await Task.WhenAll(pending);
        }
    }

    private async Task PersistProjectAsync(ProjectItemViewModel project, bool syncMetadata)
    {
        try
        {
            await _repository.SaveProjectAsync(project.ToModelSnapshot());
        }
        catch (Exception exception)
        {
            ShowStatus($"保存工程数据失败：{exception.Message}");
        }

        if (!syncMetadata || !project.IsProjectPathAvailable)
        {
            return;
        }

        try
        {
            await _metadataStore.WriteAsync(
                project.ProjectPath,
                project.ToMetadataDocument());
        }
        catch (Exception exception)
        {
            ShowStatus($"同步 _ProjectPreview 元数据失败：{exception.Message}");
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    private void OpenMediaInViewer(ProjectMediaItemViewModel? media)
    {
        if (media is null)
        {
            return;
        }

        if (media.Kind == ProjectMediaKind.Video)
        {
            _mediaViewer.ShowVideo(media.FilePath, media.FileName);
        }
        else
        {
            _mediaViewer.ShowImage(media.FilePath, media.FileName);
        }
    }

    private void GoBackToGallery()
    {
        if (Gallery is not null)
        {
            Gallery.RefreshResults();
            if (IsSettingsPageActive && _navigationBeforeSettings is not null)
            {
                _navigationBeforeSettings.IsActive = true;
            }

            IsSettingsPageActive = false;
            CurrentPage = Gallery;
            StatusMessage = "已返回项目库";
        }
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
    }
}
