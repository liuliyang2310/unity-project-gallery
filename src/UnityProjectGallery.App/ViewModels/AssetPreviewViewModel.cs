using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.Core.Models;
using UnityProjectGallery.Core.Mvvm;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>
/// Assets 预览 ViewModel：按需扫描工程 Assets，并统一处理模型、图片与音频的预览状态。
/// 模型只加载静态网格，图片按原图显示，音频交给播放控件解码。
/// </summary>
public sealed class AssetPreviewViewModel : ObservableObject, IDisposable
{
    private const string DefaultStatusMessage =
        "从 Assets 读取，只读预览：模型不加载动画/骨骼/Unity 材质，图片不应用导入设置，音频播放依赖系统音频设备。";

    private readonly IAssetsPreviewScanner _scanner;
    private readonly IModelPreviewLoader _loader;
    private readonly IMediaViewerService _mediaViewer;
    private readonly IProjectFolderRevealService _folderRevealService;
    private readonly string _projectPath;

    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;
    private bool _isInitialized;
    private bool _isScanning;
    private bool _isLoading;
    private bool _isScanError;
    private string? _errorTitle;
    private string? _errorMessage;
    private ModelPreviewScene? _scene;
    private ImageSource? _previewImageSource;
    private int _previewImageWidth;
    private int _previewImageHeight;
    private string? _previewAudioPath;
    private AssetPreviewItemViewModel? _selectedAsset;
    private AssetPreviewCategoryItemViewModel? _selectedCategory;
    private string _searchText = string.Empty;
    private string _statusMessage = DefaultStatusMessage;
    private bool _isDisposed;

    public AssetPreviewViewModel(
        IAssetsPreviewScanner scanner,
        IModelPreviewLoader loader,
        IMediaViewerService mediaViewer,
        IProjectFolderRevealService folderRevealService,
        string projectName,
        string projectPath)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(mediaViewer);
        ArgumentNullException.ThrowIfNull(folderRevealService);
        ArgumentNullException.ThrowIfNull(projectName);
        ArgumentNullException.ThrowIfNull(projectPath);

        _scanner = scanner;
        _loader = loader;
        _mediaViewer = mediaViewer;
        _folderRevealService = folderRevealService;
        ProjectName = projectName;
        _projectPath = projectPath;

        AssetsView = CollectionViewSource.GetDefaultView(Assets);
        AssetsView.Filter = FilterAsset;

        Categories = new ObservableCollection<AssetPreviewCategoryItemViewModel>
        {
            new("全部", null),
            new("模型", AssetPreviewKind.Model),
            new("图片", AssetPreviewKind.Image),
            new("音频", AssetPreviewKind.Audio)
        };
        _selectedCategory = Categories[0];

        RefreshCommand = new RelayCommand(() => _ = InitializeAsync(forceRefresh: true));
        RetryCommand = new RelayCommand(() => _ = RetryAsync());
        OpenImageCommand = new RelayCommand(OpenImage);
        RevealAssetFolderCommand = new RelayCommand(RevealAssetFolder);
    }

    public string ProjectName { get; }

    public ObservableCollection<AssetPreviewItemViewModel> Assets { get; } = new();

    public ICollectionView AssetsView { get; }

    public ObservableCollection<AssetPreviewCategoryItemViewModel> Categories { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand RetryCommand { get; }

    public RelayCommand OpenImageCommand { get; }

    public RelayCommand RevealAssetFolderCommand { get; }

    public ModelPreviewScene? Scene
    {
        get => _scene;
        private set
        {
            if (SetProperty(ref _scene, value))
            {
                OnPropertyChanged(nameof(HasScene));
                OnPropertyChanged(nameof(PreviewSummary));
            }
        }
    }

    public ImageSource? PreviewImageSource
    {
        get => _previewImageSource;
        private set => SetProperty(ref _previewImageSource, value);
    }

    public string? PreviewAudioPath
    {
        get => _previewAudioPath;
        private set => SetProperty(ref _previewAudioPath, value);
    }

    public AssetPreviewItemViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (!SetProperty(ref _selectedAsset, value))
            {
                return;
            }

            StatusMessage = DefaultStatusMessage;
            RaiseViewState();
            _ = LoadSelectedAssetAsync(value);
        }
    }

    public AssetPreviewCategoryItemViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (value is null || !SetProperty(ref _selectedCategory, value))
            {
                return;
            }

            RefreshFilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            RefreshFilter();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy => _isScanning || _isLoading;

    public bool HasScene => Scene is not null;

    public bool HasError => _errorMessage is not null;

    public bool HasNoVisibleAssets =>
        _isInitialized && AssetsView.IsEmpty && !HasError && !_isScanning;

    public bool ShowSelectionHint =>
        _isInitialized && !AssetsView.IsEmpty && _selectedAsset is null &&
        !IsBusy && !HasError;

    public bool IsModelSelected => _selectedAsset?.Kind == AssetPreviewKind.Model;

    public bool IsImageSelected => _selectedAsset?.Kind == AssetPreviewKind.Image;

    public bool IsAudioSelected => _selectedAsset?.Kind == AssetPreviewKind.Audio;

    public bool CanOpenImage => IsImageSelected;

    public bool CanReloadPreview => IsModelSelected || IsImageSelected;

    public bool CanRevealAssetFolder => _selectedAsset is not null;

    public string BusyText => _isScanning
        ? "正在扫描 Assets 目录…"
        : _selectedAsset?.Kind switch
        {
            AssetPreviewKind.Image => $"正在解码 {_selectedAsset.FileName}…",
            AssetPreviewKind.Model => $"正在加载 {_selectedAsset.FileName}…",
            _ => "正在加载资源…"
        };

    public string ErrorTitle => _errorTitle ?? "无法预览该资源";

    public string ErrorMessage => _errorMessage ?? string.Empty;

    public string AssetCountText => $"{Assets.Count} 个资源";

    public string CurrentKindText => _selectedAsset?.KindText ?? "资源";

    public string CurrentFileName => _selectedAsset?.FileName ?? "未选择资源";

    public string CurrentFilePath => _selectedAsset?.RelativePath ?? string.Empty;

    public string PreviewSummary
    {
        get
        {
            if (_selectedAsset is null)
            {
                return string.Empty;
            }

            return _selectedAsset.Kind switch
            {
                AssetPreviewKind.Model => Scene is null
                    ? string.Empty
                    : $"{Scene.MeshCount} 个网格 · {Scene.VertexCount:N0} 顶点 · {Scene.TriangleCount:N0} 三角面",
                AssetPreviewKind.Image => _previewImageWidth > 0
                    ? $"图片 · {_previewImageWidth} × {_previewImageHeight}"
                    : "图片",
                AssetPreviewKind.Audio => $"音频 · {_selectedAsset.ExtensionText}",
                _ => string.Empty
            };
        }
    }

    public async Task InitializeAsync(bool forceRefresh = false)
    {
        if (_isDisposed || (_isInitialized && !forceRefresh))
        {
            return;
        }

        CancelLoad();
        ResetPreviewState();
        Assets.Clear();
        UpdateCategoryCounts();

        _isInitialized = false;
        _isScanning = true;
        _isScanError = false;
        _errorTitle = null;
        _errorMessage = null;
        RaiseViewState();

        try
        {
            var assets = await _scanner.ScanAsync(_projectPath);
            if (_isDisposed)
            {
                return;
            }

            Assets.Clear();
            foreach (var asset in assets)
            {
                Assets.Add(new AssetPreviewItemViewModel(asset));
            }

            UpdateCategoryCounts();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _isScanError = true;
            _errorTitle = "扫描 Assets 失败";
            _errorMessage = exception.Message;
        }
        finally
        {
            if (!_isDisposed)
            {
                _isScanning = false;
                _isInitialized = true;
                AssetsView.Refresh();
                RaiseViewState();
            }
        }

        if (_isDisposed || _errorMessage is not null)
        {
            return;
        }

        EnsureVisibleSelection();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        CancelLoad();
        PreviewAudioPath = null;
        PreviewImageSource = null;
        Scene = null;
    }

    private async Task RetryAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_isScanError)
        {
            await InitializeAsync(forceRefresh: true);
            return;
        }

        if (_selectedAsset is null)
        {
            if (_errorMessage is not null)
            {
                await InitializeAsync(forceRefresh: true);
            }

            return;
        }

        if (_selectedAsset.Kind == AssetPreviewKind.Audio)
        {
            return;
        }

        await LoadSelectedAssetAsync(_selectedAsset);
    }

    private void OpenImage()
    {
        if (_selectedAsset?.Kind != AssetPreviewKind.Image)
        {
            return;
        }

        _mediaViewer.ShowImage(_selectedAsset.FilePath, _selectedAsset.FileName);
    }

    private void RevealAssetFolder()
    {
        if (_selectedAsset is null)
        {
            return;
        }

        var folderPath = Path.GetDirectoryName(_selectedAsset.FilePath);
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            StatusMessage = $"文件夹不存在：{folderPath}";
            return;
        }

        StatusMessage = _folderRevealService.Reveal(folderPath)
            ? $"已在资源管理器中打开：{folderPath}"
            : "无法打开资源管理器，请检查系统文件关联。";
    }

    private void RefreshFilter()
    {
        AssetsView.Refresh();
        RaiseViewState();
        EnsureVisibleSelection();
    }

    private void EnsureVisibleSelection()
    {
        var visibleAssets = AssetsView
            .Cast<AssetPreviewItemViewModel>()
            .ToList();

        if (_selectedAsset is not null && visibleAssets.Contains(_selectedAsset))
        {
            return;
        }

        SelectedAsset = visibleAssets.FirstOrDefault();
    }

    private void UpdateCategoryCounts()
    {
        foreach (var category in Categories)
        {
            category.Count = category.Kind is { } kind
                ? Assets.Count(asset => asset.Kind == kind)
                : Assets.Count;
        }
    }

    private bool FilterAsset(object item)
    {
        if (item is not AssetPreviewItemViewModel asset)
        {
            return false;
        }

        if (_selectedCategory?.Kind is { } kind && asset.Kind != kind)
        {
            return false;
        }

        var keyword = SearchText.Trim();
        return keyword.Length == 0 || asset.Matches(keyword);
    }

    private async Task LoadSelectedAssetAsync(AssetPreviewItemViewModel? asset)
    {
        CancelLoad();
        var version = ++_loadVersion;

        if (asset is null || _isDisposed)
        {
            ResetPreviewState();
            return;
        }

        Scene = null;
        PreviewImageSource = null;
        _previewImageWidth = 0;
        _previewImageHeight = 0;
        PreviewAudioPath = null;
        _errorTitle = null;
        _errorMessage = null;
        _isLoading = false;
        RaiseViewState();

        if (asset.Kind == AssetPreviewKind.Audio)
        {
            PreviewAudioPath = asset.FilePath;
            RaiseViewState();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _isLoading = true;
        RaiseViewState();

        try
        {
            if (asset.Kind == AssetPreviewKind.Model)
            {
                var result = await _loader.LoadAsync(asset.FilePath, cancellation.Token);
                if (version != _loadVersion || _isDisposed)
                {
                    return;
                }

                if (result.Scene is not null)
                {
                    Scene = result.Scene;
                }
                else
                {
                    _errorTitle = "无法预览该模型";
                    _errorMessage = result.ErrorMessage ?? "模型解析失败。";
                }
            }
            else if (asset.Kind == AssetPreviewKind.Image)
            {
                var result = await Task.Run(
                    () => LoadImage(asset.FilePath),
                    cancellation.Token);
                if (version != _loadVersion || _isDisposed)
                {
                    return;
                }

                if (result.Source is not null)
                {
                    _previewImageWidth = result.Width;
                    _previewImageHeight = result.Height;
                    PreviewImageSource = result.Source;
                }
                else
                {
                    _errorTitle = "无法预览该图片";
                    _errorMessage = result.ErrorMessage ?? "图片解码失败。";
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            if (version != _loadVersion || _isDisposed)
            {
                return;
            }

            _errorTitle = asset.Kind == AssetPreviewKind.Image
                ? "无法预览该图片"
                : "无法预览该模型";
            _errorMessage = exception.Message;
        }
        finally
        {
            if (version == _loadVersion && !_isDisposed)
            {
                _isLoading = false;
                RaiseViewState();
            }
        }
    }

    private void ResetPreviewState()
    {
        Scene = null;
        PreviewImageSource = null;
        _previewImageWidth = 0;
        _previewImageHeight = 0;
        PreviewAudioPath = null;
        _errorTitle = null;
        _errorMessage = null;
        _isScanError = false;
        _isLoading = false;
        RaiseViewState();
    }

    private void CancelLoad()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        _loadVersion++;
    }

    private static ImagePreviewResult LoadImage(string filePath)
    {
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return new ImagePreviewResult(null, 0, 0, "图片中没有可显示的帧。");
            }

            var frame = decoder.Frames[0];
            frame.Freeze();
            return new ImagePreviewResult(frame, frame.PixelWidth, frame.PixelHeight, null);
        }
        catch (Exception exception)
        {
            return new ImagePreviewResult(
                null,
                0,
                0,
                $"无法解码该图片，可能是格式不受支持或文件已损坏。{Environment.NewLine}{exception.Message}");
        }
    }

    private void RaiseViewState()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(HasScene));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasNoVisibleAssets));
        OnPropertyChanged(nameof(ShowSelectionHint));
        OnPropertyChanged(nameof(IsModelSelected));
        OnPropertyChanged(nameof(IsImageSelected));
        OnPropertyChanged(nameof(IsAudioSelected));
        OnPropertyChanged(nameof(CanOpenImage));
        OnPropertyChanged(nameof(CanReloadPreview));
        OnPropertyChanged(nameof(CanRevealAssetFolder));
        OnPropertyChanged(nameof(BusyText));
        OnPropertyChanged(nameof(ErrorTitle));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(AssetCountText));
        OnPropertyChanged(nameof(CurrentKindText));
        OnPropertyChanged(nameof(CurrentFileName));
        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(PreviewSummary));
    }

    private sealed record ImagePreviewResult(
        ImageSource? Source,
        int Width,
        int Height,
        string? ErrorMessage);
}
