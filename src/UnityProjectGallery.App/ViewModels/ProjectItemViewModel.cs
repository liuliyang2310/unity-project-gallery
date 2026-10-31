using System.Collections.ObjectModel;
using System.IO;
using UnityProjectGallery.Core.Media;
using UnityProjectGallery.Core.Mvvm;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>
/// 工程卡片的展示单元。收藏状态在界面上共享同一实例，因此详情页与列表页始终一致。
/// 重新扫描时，仅刷新扫描相关字段，保留用户维护的封面与素材显示状态。
/// </summary>
public sealed class ProjectItemViewModel : ObservableObject
{
    private readonly List<ProjectMediaItemViewModel> _media = new();
    private readonly Action<ProjectMediaItemViewModel> _openMedia;
    private readonly Action<ProjectItemViewModel, bool> _stateChanged;
    private string _projectPath;
    private bool _isProjectPathAvailable;
    private bool _isFavorite;
    private bool _showHiddenMedia;
    private bool _isCoverManual;
    private string _unityVersion;
    private string _description;
    private IReadOnlyList<string> _tags;
    private IReadOnlyList<UnityPackageReference> _packages;
    private DateTimeOffset? _lastOpenedTime;
    private DateTimeOffset? _lastModifiedTime;
    private DateTimeOffset? _lastScanTime;
    private DateTimeOffset? _userDataUpdatedAt;
    private int _screenshotCount;
    private int _videoCount;
    private ProjectMediaItemViewModel? _coverMedia;
    private string? _mediaWarningText;

    public ProjectItemViewModel(
        UnityProject project,
        Action<ProjectItemViewModel> openProject,
        Action<ProjectMediaItemViewModel> openMedia,
        Action<ProjectItemViewModel, bool>? stateChanged = null,
        ProjectMetadataDocument? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(openProject);
        ArgumentNullException.ThrowIfNull(openMedia);

        _openMedia = openMedia;
        _stateChanged = stateChanged ?? ((_, _) => { });

        // 工程目录内的 JSON 比 SQLite 快照新（或快照没有用户编辑时间）时，
        // 优先恢复 JSON 中的用户内容。
        var effectiveProject = metadata is not null &&
            (project.UserDataUpdatedAt is null ||
             metadata.SavedAt >= project.UserDataUpdatedAt.Value)
            ? ApplyMetadata(project, metadata)
            : project;

        Id = project.Id;
        Name = project.Name;
        _projectPath = project.ProjectPath;
        _isProjectPathAvailable = IsUnityProjectDirectory(project.ProjectPath);
        _unityVersion = effectiveProject.UnityVersion;
        _description = effectiveProject.Description;
        _tags = effectiveProject.Tags;
        _packages = effectiveProject.Packages;
        _isFavorite = effectiveProject.IsFavorite;
        _lastOpenedTime = effectiveProject.LastOpenedTime;
        _lastModifiedTime = effectiveProject.LastModifiedTime;
        _lastScanTime = effectiveProject.LastScanTime;
        _userDataUpdatedAt = effectiveProject.UserDataUpdatedAt;
        _screenshotCount = effectiveProject.ScreenshotCount;
        _videoCount = effectiveProject.VideoCount;

        OpenCommand = new RelayCommand(() => openProject(this));
        ToggleFavoriteCommand = new RelayCommand(ToggleFavorite);
        ToggleHiddenMediaCommand = new RelayCommand(ToggleHiddenMedia);

        RestorePersistedContent(effectiveProject);
    }

    public string Id { get; }

    public string Name { get; }

    public string ProjectPath
    {
        get => _projectPath;
        private set => SetProperty(ref _projectPath, value);
    }

    public bool IsProjectPathAvailable
    {
        get => _isProjectPathAvailable;
        private set
        {
            if (SetProperty(ref _isProjectPathAvailable, value))
            {
                OnPropertyChanged(nameof(IsProjectPathMissing));
            }
        }
    }

    public bool IsProjectPathMissing => !IsProjectPathAvailable;

    public string UnityVersion
    {
        get => _unityVersion;
        private set
        {
            if (SetProperty(ref _unityVersion, value))
            {
                OnPropertyChanged(nameof(UnityVersionDisplay));
            }
        }
    }

    public string UnityVersionDisplay => $"Unity {UnityVersion}";

    public string Description
    {
        get => _description;
        private set
        {
            if (SetProperty(ref _description, value))
            {
                OnPropertyChanged(nameof(DescriptionDisplay));
            }
        }
    }

    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description)
        ? "暂无简介，可在详情页编辑。"
        : Description;

    public IReadOnlyList<string> Tags
    {
        get => _tags;
        private set => SetProperty(ref _tags, value);
    }

    public ObservableCollection<ProjectFeatureViewModel> Features { get; } = new();

    public bool HasFeatures => Features.Count > 0;

    public int FeatureCount => Features.Count;

    public string FeatureSummary => $"{FeatureCount} 个功能";

    public IReadOnlyList<UnityPackageReference> Packages
    {
        get => _packages;
        private set
        {
            if (SetProperty(ref _packages, value))
            {
                OnPropertyChanged(nameof(PackageCount));
                OnPropertyChanged(nameof(PackageSummary));
                OnPropertyChanged(nameof(HasPackages));
            }
        }
    }

    public int PackageCount => Packages.Count;

    public bool HasPackages => Packages.Count > 0;

    public string PackageSummary => Packages.Count == 0
        ? "未读取到依赖包"
        : $"{Packages.Count} 个依赖包";

    public DateTimeOffset? LastOpenedTime
    {
        get => _lastOpenedTime;
        private set => SetProperty(ref _lastOpenedTime, value);
    }

    public DateTimeOffset? LastModifiedTime
    {
        get => _lastModifiedTime;
        private set => SetProperty(ref _lastModifiedTime, value);
    }

    public DateTimeOffset? LastScanTime
    {
        get => _lastScanTime;
        private set => SetProperty(ref _lastScanTime, value);
    }

    public int ScreenshotCount
    {
        get => _screenshotCount;
        private set
        {
            if (SetProperty(ref _screenshotCount, value))
            {
                OnPropertyChanged(nameof(MediaSummary));
            }
        }
    }

    public int VideoCount
    {
        get => _videoCount;
        private set
        {
            if (SetProperty(ref _videoCount, value))
            {
                OnPropertyChanged(nameof(MediaSummary));
            }
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        private set => SetProperty(ref _isFavorite, value);
    }

    public string MediaSummary => $"{ScreenshotCount} 张图片 · {VideoCount} 个视频";

    public ObservableCollection<ProjectMediaItemViewModel> DisplayedMedia { get; } = new();

    public IReadOnlyList<ProjectMediaItemViewModel> AllMedia => _media;

    public bool HasMedia => _media.Count > 0;

    public int MediaCount => _media.Count;

    public int HiddenMediaCount => _media.Count(media => !media.IsIncluded);

    public bool HasHiddenMedia => HiddenMediaCount > 0;

    public bool CanToggleHiddenMedia => HasHiddenMedia || _showHiddenMedia;

    public bool ShowHiddenMedia
    {
        get => _showHiddenMedia;
        private set => SetProperty(ref _showHiddenMedia, value);
    }

    public string ShowHiddenButtonText => ShowHiddenMedia
        ? "仅显示展示素材"
        : $"显示已移除 ({HiddenMediaCount})";

    public string? CoverThumbnailPath
    {
        get
        {
            if (_coverMedia is null)
            {
                return null;
            }

            return _coverMedia.ThumbnailPath
                ?? (_coverMedia.IsImage ? _coverMedia.FilePath : null);
        }
    }

    public string? CoverImagePath
    {
        get
        {
            if (_coverMedia is null)
            {
                return null;
            }

            return _coverMedia.IsImage
                ? _coverMedia.FilePath
                : _coverMedia.ThumbnailPath;
        }
    }

    public bool HasCoverThumbnail => !string.IsNullOrWhiteSpace(CoverThumbnailPath);

    public bool HasCoverImage => !string.IsNullOrWhiteSpace(CoverImagePath);

    public string? MediaWarningText
    {
        get => _mediaWarningText;
        private set
        {
            if (SetProperty(ref _mediaWarningText, value))
            {
                OnPropertyChanged(nameof(HasMediaWarnings));
            }
        }
    }

    public bool HasMediaWarnings => !string.IsNullOrWhiteSpace(MediaWarningText);

    public RelayCommand OpenCommand { get; }

    public RelayCommand ToggleFavoriteCommand { get; }

    public RelayCommand ToggleHiddenMediaCommand { get; }

    /// <summary>用一次新扫描的结果更新工程识别信息。</summary>
    public void ApplyScanResult(UnityProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        UnityVersion = project.UnityVersion;
        Packages = project.Packages;
        LastModifiedTime = project.LastModifiedTime;
        LastScanTime = project.LastScanTime;
    }

    /// <summary>用一次新扫描的结果更新素材列表，并保留已隐藏素材与手动封面状态。</summary>
    public void ApplyMediaResult(ProjectMediaScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var previousCoverPath = _coverMedia?.FilePath;
        var previousMedia = _media.ToDictionary(media => media.FilePath, StringComparer.OrdinalIgnoreCase);
        var newMedia = new List<ProjectMediaItemViewModel>(result.Media.Count);

        foreach (var asset in result.Media)
        {
            if (previousMedia.TryGetValue(asset.FilePath, out var existing))
            {
                existing.ApplyAsset(asset);
                newMedia.Add(existing);
            }
            else
            {
                newMedia.Add(new ProjectMediaItemViewModel(
                    asset,
                    _openMedia,
                    ToggleMediaInclusion,
                    SetMediaAsCover));
            }
        }

        _media.Clear();
        _media.AddRange(newMedia);

        // 清理已不存在素材的功能关联，避免保留失效引用。
        var currentMedia = _media.ToHashSet();
        foreach (var feature in Features)
        {
            foreach (var linkedMedia in feature.Media.ToList())
            {
                if (!currentMedia.Contains(linkedMedia))
                {
                    linkedMedia.SetFeature(null);
                    feature.RemoveMedia(linkedMedia);
                }
            }
        }

        ScreenshotCount = _media.Count(media => media.IsImage);
        VideoCount = _media.Count(media => media.IsVideo);
        MediaWarningText = result.Warnings.Count == 0
            ? null
            : string.Join(Environment.NewLine, result.Warnings);

        ProjectMediaItemViewModel? manualCover = null;
        if (_isCoverManual && previousCoverPath is not null)
        {
            manualCover = _media.FirstOrDefault(media =>
                string.Equals(media.FilePath, previousCoverPath, StringComparison.OrdinalIgnoreCase));
        }

        if (manualCover is not null)
        {
            ApplyCover(manualCover);
        }
        else
        {
            _isCoverManual = false;
            SelectAutomaticCover();
        }

        RebuildDisplayedMedia();
        RaiseMediaProperties();
        NotifyStateChanged(isUserEdit: false);
    }

    private void SetMediaAsCover(ProjectMediaItemViewModel? media)
    {
        if (media is null)
        {
            return;
        }

        if (!media.IsIncluded)
        {
            media.SetIncluded(true);
            RebuildDisplayedMedia();
            RaiseMediaProperties();
        }

        _isCoverManual = true;
        ApplyCover(media);
        NotifyStateChanged();
    }

    private void ToggleMediaInclusion(ProjectMediaItemViewModel media)
    {
        media.SetIncluded(!media.IsIncluded);

        if (!media.IsIncluded && ReferenceEquals(_coverMedia, media))
        {
            _isCoverManual = false;
            SelectAutomaticCover();
        }

        RebuildDisplayedMedia();
        RaiseMediaProperties();
        NotifyStateChanged();
    }

    private void ToggleHiddenMedia()
    {
        if (!CanToggleHiddenMedia)
        {
            return;
        }

        ShowHiddenMedia = !ShowHiddenMedia;
        RebuildDisplayedMedia();
        RaiseMediaProperties();
    }

    private void RebuildDisplayedMedia()
    {
        DisplayedMedia.Clear();
        foreach (var media in _media)
        {
            if (ShowHiddenMedia || media.IsIncluded)
            {
                DisplayedMedia.Add(media);
            }
        }
    }

    private void SelectAutomaticCover()
    {
        var candidates = _media.Where(media => media.IsIncluded).ToList();
        var rootMedia = candidates
            .Where(media => !media.RelativePath.Contains('\\') && !media.RelativePath.Contains('/'))
            .ToList();

        // 默认封面优先使用 _ProjectPreview 第一层的图片，避免子目录素材抢占封面。
        var cover = rootMedia.FirstOrDefault(media => media.IsImage && media.HasThumbnail)
            ?? rootMedia.FirstOrDefault(media => media.IsImage)
            ?? candidates.FirstOrDefault(media => media.IsImage && media.HasThumbnail)
            ?? candidates.FirstOrDefault(media => media.IsImage)
            ?? candidates.FirstOrDefault(media => media.HasThumbnail)
            ?? candidates.FirstOrDefault();

        ApplyCover(cover);
    }

    private void ApplyCover(ProjectMediaItemViewModel? media)
    {
        foreach (var item in _media)
        {
            item.IsCover = ReferenceEquals(item, media);
        }

        _coverMedia = media;
        OnPropertyChanged(nameof(CoverThumbnailPath));
        OnPropertyChanged(nameof(CoverImagePath));
        OnPropertyChanged(nameof(HasCoverThumbnail));
        OnPropertyChanged(nameof(HasCoverImage));
    }

    private void RaiseMediaProperties()
    {
        OnPropertyChanged(nameof(HasMedia));
        OnPropertyChanged(nameof(MediaCount));
        OnPropertyChanged(nameof(HiddenMediaCount));
        OnPropertyChanged(nameof(HasHiddenMedia));
        OnPropertyChanged(nameof(CanToggleHiddenMedia));
        OnPropertyChanged(nameof(ShowHiddenButtonText));
        OnPropertyChanged(nameof(MediaSummary));
    }

    public void SetDescription(string? description)
    {
        Description = description?.Trim() ?? string.Empty;
        NotifyStateChanged();
    }

    /// <summary>记录一次成功启动工程的时间。</summary>
    public void MarkOpened(DateTimeOffset openedAt)
    {
        LastOpenedTime = openedAt;
        NotifyStateChanged();
    }

    public void SetTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        Tags = tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        NotifyStateChanged();
    }

    public void SetMediaDescription(ProjectMediaItemViewModel media, string? description)
    {
        ArgumentNullException.ThrowIfNull(media);
        media.SetDescription(description);
        NotifyStateChanged();
    }

    public ProjectFeatureViewModel CreateFeature(string name, string description)
    {
        var feature = new ProjectFeatureViewModel(name.Trim(), description.Trim());
        Features.Add(feature);
        RaiseFeatureProperties();
        NotifyStateChanged();
        return feature;
    }

    public void RemoveFeature(ProjectFeatureViewModel feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        if (!Features.Contains(feature))
        {
            return;
        }

        foreach (var media in feature.Media.ToList())
        {
            media.SetFeature(null);
            feature.RemoveMedia(media);
        }

        Features.Remove(feature);
        RaiseFeatureProperties();
        NotifyStateChanged();
    }

    /// <summary>将勾选的素材关联到指定功能；一条素材只保留一个功能关联。</summary>
    public void AssignMediaToFeature(
        ProjectFeatureViewModel feature,
        IEnumerable<ProjectMediaItemViewModel> selectedMedia)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(selectedMedia);
        if (!Features.Contains(feature))
        {
            return;
        }

        var selected = selectedMedia.ToHashSet();

        foreach (var linkedMedia in feature.Media.ToList())
        {
            if (!selected.Contains(linkedMedia))
            {
                linkedMedia.SetFeature(null);
                feature.RemoveMedia(linkedMedia);
            }
        }

        foreach (var media in selected)
        {
            if (!_media.Contains(media) || ReferenceEquals(media.Feature, feature))
            {
                continue;
            }

            media.Feature?.RemoveMedia(media);
            media.SetFeature(feature);
            feature.AddMedia(media);
        }

        NotifyStateChanged();
    }

    private void RaiseFeatureProperties()
    {
        OnPropertyChanged(nameof(FeatureCount));
        OnPropertyChanged(nameof(FeatureSummary));
        OnPropertyChanged(nameof(HasFeatures));
    }

    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
        NotifyStateChanged();
    }

    /// <summary>工程目录被移动后，更新工程路径与素材绝对路径。</summary>
    public bool RelinkProject(string projectPath)
    {
        var normalized = NormalizePath(projectPath);
        if (!IsUnityProjectDirectory(normalized))
        {
            return false;
        }

        ProjectPath = normalized;
        IsProjectPathAvailable = true;

        foreach (var media in _media)
        {
            media.UpdateFilePath(Path.Combine(
                normalized,
                ProjectMediaConventions.PreviewDirectoryName,
                media.RelativePath));
        }

        OnPropertyChanged(nameof(CoverImagePath));
        OnPropertyChanged(nameof(CoverThumbnailPath));
        NotifyStateChanged();
        return true;
    }

    public void RefreshPathAvailability()
    {
        IsProjectPathAvailable = IsUnityProjectDirectory(ProjectPath);
    }

    /// <summary>构造用于 SQLite 持久化的完整工程快照。</summary>
    public UnityProject ToModelSnapshot()
    {
        return new UnityProject
        {
            Id = Id,
            Name = Name,
            ProjectPath = ProjectPath,
            UnityVersion = UnityVersion,
            Description = Description,
            Tags = Tags.ToArray(),
            Packages = Packages.ToArray(),
            Features = Features.Select(feature => feature.ToModel()).ToArray(),
            Media = _media.Select(media => media.ToAsset()).ToArray(),
            CoverMediaPath = _coverMedia?.FilePath,
            IsFavorite = IsFavorite,
            LastOpenedTime = LastOpenedTime,
            UserDataUpdatedAt = _userDataUpdatedAt,
            LastModifiedTime = LastModifiedTime,
            LastScanTime = LastScanTime,
            ScreenshotCount = ScreenshotCount,
            VideoCount = VideoCount,
            FeatureCount = Features.Count
        };
    }

    /// <summary>构造写入工程 `_ProjectPreview` 的用户元数据文档。</summary>
    public ProjectMetadataDocument ToMetadataDocument()
    {
        return new ProjectMetadataDocument
        {
            SavedAt = _userDataUpdatedAt ?? DateTimeOffset.UtcNow,
            Description = Description,
            Tags = Tags.ToArray(),
            Features = Features.Select(feature => feature.ToModel()).ToArray(),
            Media = _media
                .Select(media => new ProjectMediaMetadata
                {
                    RelativePath = media.RelativePath,
                    Description = media.Description,
                    IsIncluded = media.IsIncluded,
                    FeatureId = media.Feature?.Id
                })
                .ToArray(),
            CoverRelativePath = _coverMedia?.RelativePath,
            IsFavorite = IsFavorite,
            LastOpenedTime = LastOpenedTime
        };
    }

    private void RestorePersistedContent(UnityProject project)
    {
        if (project.Features.Length > 0)
        {
            var featureMap = new Dictionary<string, ProjectFeatureViewModel>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var featureModel in project.Features)
            {
                var feature = new ProjectFeatureViewModel(
                    featureModel.Name,
                    featureModel.Description,
                    featureModel.Id);
                Features.Add(feature);
                featureMap[feature.Id] = feature;
            }

            foreach (var asset in project.Media)
            {
                var media = new ProjectMediaItemViewModel(
                    asset,
                    _openMedia,
                    ToggleMediaInclusion,
                    SetMediaAsCover);
                media.SetDescription(asset.Description);
                media.SetIncluded(asset.IsIncluded);

                if (!string.IsNullOrWhiteSpace(asset.FeatureId) &&
                    featureMap.TryGetValue(asset.FeatureId, out var feature))
                {
                    media.SetFeature(feature);
                    feature.AddMedia(media);
                }

                _media.Add(media);
            }
        }
        else
        {
            foreach (var asset in project.Media)
            {
                var media = new ProjectMediaItemViewModel(
                    asset,
                    _openMedia,
                    ToggleMediaInclusion,
                    SetMediaAsCover);
                media.SetDescription(asset.Description);
                media.SetIncluded(asset.IsIncluded);
                _media.Add(media);
            }
        }

        if (_media.Count == 0)
        {
            return;
        }

        ScreenshotCount = _media.Count(media => media.IsImage);
        VideoCount = _media.Count(media => media.IsVideo);

        var persistedCover = project.CoverMediaPath is null
            ? null
            : _media.FirstOrDefault(media => string.Equals(
                media.FilePath,
                project.CoverMediaPath,
                StringComparison.OrdinalIgnoreCase));

        if (persistedCover is not null)
        {
            _isCoverManual = true;
            ApplyCover(persistedCover);
        }
        else
        {
            SelectAutomaticCover();
        }

        RebuildDisplayedMedia();
        RaiseMediaProperties();
    }

    private void NotifyStateChanged(bool isUserEdit = true)
    {
        if (isUserEdit)
        {
            _userDataUpdatedAt = DateTimeOffset.UtcNow;
        }

        _stateChanged(this, isUserEdit);
    }

    /// <summary>把工程目录 JSON 中的用户内容覆盖到扫描得到的工程模型上。</summary>
    private static UnityProject ApplyMetadata(
        UnityProject project,
        ProjectMetadataDocument metadata)
    {
        var metadataMedia = metadata.Media ?? Array.Empty<ProjectMediaMetadata>();
        var metadataFeatures = metadata.Features ?? Array.Empty<ProjectFeature>();

        var metadataByPath = metadataMedia
            .Where(entry => !string.IsNullOrWhiteSpace(entry.RelativePath))
            .GroupBy(
                entry => NormalizeRelativePath(entry.RelativePath),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);

        var media = project.Media
            .Select(asset =>
            {
                if (!metadataByPath.TryGetValue(
                        NormalizeRelativePath(asset.RelativePath),
                        out var entry))
                {
                    return asset;
                }

                return new ProjectMediaAsset
                {
                    FilePath = asset.FilePath,
                    FileName = asset.FileName,
                    Kind = asset.Kind,
                    RelativePath = asset.RelativePath,
                    PixelWidth = asset.PixelWidth,
                    PixelHeight = asset.PixelHeight,
                    DurationSeconds = asset.DurationSeconds,
                    ThumbnailPath = asset.ThumbnailPath,
                    Description = entry.Description,
                    IsIncluded = entry.IsIncluded,
                    FeatureId = entry.FeatureId
                };
            })
            .ToArray();

        var coverPath = FindMediaPath(media, metadata.CoverRelativePath);

        return new UnityProject
        {
            Id = project.Id,
            Name = project.Name,
            ProjectPath = project.ProjectPath,
            UnityVersion = project.UnityVersion,
            Description = metadata.Description ?? string.Empty,
            Tags = metadata.Tags ?? Array.Empty<string>(),
            Packages = project.Packages,
            Features = metadataFeatures,
            Media = media,
            CoverMediaPath = coverPath,
            IsFavorite = metadata.IsFavorite,
            LastOpenedTime = metadata.LastOpenedTime,
            UserDataUpdatedAt = metadata.SavedAt,
            LastModifiedTime = project.LastModifiedTime,
            LastScanTime = project.LastScanTime,
            ScreenshotCount = media.Count(asset => asset.Kind == ProjectMediaKind.Image),
            VideoCount = media.Count(asset => asset.Kind == ProjectMediaKind.Video),
            FeatureCount = metadataFeatures.Length
        };
    }

    private static string? FindMediaPath(
        IEnumerable<ProjectMediaAsset> media,
        string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalized = NormalizeRelativePath(relativePath);
        return media
            .FirstOrDefault(asset => string.Equals(
                NormalizeRelativePath(asset.RelativePath),
                normalized,
                StringComparison.OrdinalIgnoreCase))
            ?.FilePath;
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    private static bool IsUnityProjectDirectory(string path)
    {
        return Directory.Exists(Path.Combine(path, "Assets")) &&
               Directory.Exists(Path.Combine(path, "Packages")) &&
               Directory.Exists(Path.Combine(path, "ProjectSettings"));
    }
}
