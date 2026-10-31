using System.IO;
using UnityProjectGallery.Core.Mvvm;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>一条展示素材的展示状态、封面与移除操作。</summary>
public sealed class ProjectMediaItemViewModel : ObservableObject
{
    private string _filePath;
    private bool _isIncluded = true;
    private bool _isCover;
    private string? _thumbnailPath;
    private int? _pixelWidth;
    private int? _pixelHeight;
    private double? _durationSeconds;
    private string _description = string.Empty;
    private ProjectFeatureViewModel? _feature;

    public ProjectMediaItemViewModel(
        ProjectMediaAsset asset,
        Action<ProjectMediaItemViewModel> openMedia,
        Action<ProjectMediaItemViewModel> toggleInclusion,
        Action<ProjectMediaItemViewModel> setCover)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(openMedia);
        ArgumentNullException.ThrowIfNull(toggleInclusion);
        ArgumentNullException.ThrowIfNull(setCover);

        _filePath = asset.FilePath;
        FileName = asset.FileName;
        RelativePath = asset.RelativePath;
        Kind = asset.Kind;
        _thumbnailPath = asset.ThumbnailPath;
        _pixelWidth = asset.PixelWidth;
        _pixelHeight = asset.PixelHeight;
        _durationSeconds = asset.DurationSeconds;

        OpenCommand = new RelayCommand(() => openMedia(this));
        ToggleInclusionCommand = new RelayCommand(() => toggleInclusion(this));
        SetCoverCommand = new RelayCommand(() => setCover(this));
    }

    public string FilePath
    {
        get => _filePath;
        private set => SetProperty(ref _filePath, value);
    }

    public string FileName { get; private set; }

    public string RelativePath { get; private set; }

    public ProjectMediaKind Kind { get; }

    public bool IsImage => Kind == ProjectMediaKind.Image;

    public bool IsVideo => Kind == ProjectMediaKind.Video;

    public string KindLabel => IsVideo ? "视频" : "图片";

    public string? ThumbnailPath
    {
        get => _thumbnailPath;
        private set
        {
            if (SetProperty(ref _thumbnailPath, value))
            {
                OnPropertyChanged(nameof(HasThumbnail));
            }
        }
    }

    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath);

    public int? PixelWidth
    {
        get => _pixelWidth;
        private set
        {
            if (SetProperty(ref _pixelWidth, value))
            {
                OnPropertyChanged(nameof(ResolutionText));
            }
        }
    }

    public int? PixelHeight
    {
        get => _pixelHeight;
        private set
        {
            if (SetProperty(ref _pixelHeight, value))
            {
                OnPropertyChanged(nameof(ResolutionText));
            }
        }
    }

    public double? DurationSeconds
    {
        get => _durationSeconds;
        private set
        {
            if (SetProperty(ref _durationSeconds, value))
            {
                OnPropertyChanged(nameof(DurationText));
                OnPropertyChanged(nameof(MetadataText));
            }
        }
    }

    public string ResolutionText => PixelWidth is > 0 && PixelHeight is > 0
        ? $"{PixelWidth} × {PixelHeight}"
        : "尺寸未知";

    public string DurationText
    {
        get
        {
            if (DurationSeconds is null or <= 0)
            {
                return "时长未知";
            }

            var duration = TimeSpan.FromSeconds(DurationSeconds.Value);
            return duration.TotalHours >= 1
                ? duration.ToString(@"hh\:mm\:ss")
                : duration.ToString(@"mm\:ss");
        }
    }

    public string MetadataText => IsVideo
        ? $"{KindLabel} · {DurationText} · {ResolutionText}"
        : $"{KindLabel} · {ResolutionText}";

    public bool IsIncluded
    {
        get => _isIncluded;
        private set => SetProperty(ref _isIncluded, value);
    }

    public bool IsCover
    {
        get => _isCover;
        internal set => SetProperty(ref _isCover, value);
    }

    public string Description
    {
        get => _description;
        private set
        {
            if (SetProperty(ref _description, value))
            {
                OnPropertyChanged(nameof(HasDescription));
            }
        }
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public ProjectFeatureViewModel? Feature
    {
        get => _feature;
        private set
        {
            if (SetProperty(ref _feature, value))
            {
                OnPropertyChanged(nameof(FeatureName));
                OnPropertyChanged(nameof(HasFeature));
            }
        }
    }

    public string FeatureName => Feature?.Name ?? string.Empty;

    public bool HasFeature => Feature is not null;

    public RelayCommand OpenCommand { get; }

    public RelayCommand ToggleInclusionCommand { get; }

    public RelayCommand SetCoverCommand { get; }

    public void ApplyAsset(ProjectMediaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        FileName = asset.FileName;
        RelativePath = asset.RelativePath;
        ThumbnailPath = asset.ThumbnailPath;
        PixelWidth = asset.PixelWidth;
        PixelHeight = asset.PixelHeight;
        DurationSeconds = asset.DurationSeconds;

        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(RelativePath));
    }

    internal void SetIncluded(bool included)
    {
        IsIncluded = included;
    }

    public void SetDescription(string? description)
    {
        Description = description?.Trim() ?? string.Empty;
    }

    internal void SetFeature(ProjectFeatureViewModel? feature)
    {
        Feature = feature;
    }

    internal void NotifyFeatureChanged()
    {
        OnPropertyChanged(nameof(FeatureName));
        OnPropertyChanged(nameof(HasFeature));
    }

    internal void UpdateFilePath(string filePath)
    {
        FilePath = filePath;
    }

    public ProjectMediaAsset ToAsset()
    {
        return new ProjectMediaAsset
        {
            FilePath = FilePath,
            FileName = FileName,
            Kind = Kind,
            RelativePath = RelativePath,
            PixelWidth = PixelWidth,
            PixelHeight = PixelHeight,
            DurationSeconds = DurationSeconds,
            ThumbnailPath = ThumbnailPath,
            Description = Description,
            IsIncluded = IsIncluded,
            FeatureId = Feature?.Id
        };
    }
}
