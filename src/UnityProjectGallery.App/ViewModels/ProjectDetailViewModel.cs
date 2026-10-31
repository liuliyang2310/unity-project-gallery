using System.Collections.ObjectModel;
using System.IO;
using UnityProjectGallery.App.Services;
using UnityProjectGallery.Core.Mvvm;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>项目详情页：展示扫描信息，并提供简介、标签、功能与素材说明编辑。</summary>
public sealed class ProjectDetailViewModel : ObservableObject
{
    private readonly Action _goBack;
    private readonly Action<string> _showStatus;
    private readonly IProjectLaunchService _projectLaunchService;
    private readonly IProjectPathPicker _projectPathPicker;
    private readonly IProjectFolderRevealService _folderRevealService;

    private bool _isLaunching;
    private bool _isEditingDescription;
    private string _descriptionDraft = string.Empty;
    private bool _isEditingTags;
    private string _tagsDraft = string.Empty;
    private bool _isEditingFeature;
    private ProjectFeatureViewModel? _editingFeature;
    private string _featureNameDraft = string.Empty;
    private string _featureDescriptionDraft = string.Empty;
    private ProjectMediaItemViewModel? _editingMedia;
    private string _mediaDescriptionDraft = string.Empty;

    public ProjectDetailViewModel(
        ProjectItemViewModel project,
        Action goBack,
        Action<string> showStatus,
        IProjectLaunchService projectLaunchService,
        IProjectPathPicker projectPathPicker,
        IProjectFolderRevealService folderRevealService)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(goBack);
        ArgumentNullException.ThrowIfNull(showStatus);
        ArgumentNullException.ThrowIfNull(projectLaunchService);
        ArgumentNullException.ThrowIfNull(projectPathPicker);
        ArgumentNullException.ThrowIfNull(folderRevealService);

        Project = project;
        _goBack = goBack;
        _showStatus = showStatus;
        _projectLaunchService = projectLaunchService;
        _projectPathPicker = projectPathPicker;
        _folderRevealService = folderRevealService;

        BackCommand = new RelayCommand(_goBack);
        OpenProjectCommand = new RelayCommand(() => _ = LaunchProjectAsync());
        RelinkProjectCommand = new RelayCommand(RelinkProject);
        RevealProjectCommand = new RelayCommand(RevealProject);

        EditDescriptionCommand = new RelayCommand(BeginEditDescription);
        SaveDescriptionCommand = new RelayCommand(SaveDescription);
        CancelDescriptionCommand = new RelayCommand(CancelDescription);
        EditTagsCommand = new RelayCommand(BeginEditTags);
        SaveTagsCommand = new RelayCommand(SaveTags);
        CancelTagsCommand = new RelayCommand(CancelTags);
        AddFeatureCommand = new RelayCommand(BeginAddFeature);
        SaveFeatureCommand = new RelayCommand(SaveFeature);
        CancelFeatureCommand = new RelayCommand(CancelFeature);
        SaveMediaDescriptionCommand = new RelayCommand(SaveMediaDescription);
        CancelMediaCommand = new RelayCommand(CancelMedia);
    }

    public ProjectItemViewModel Project { get; }

    public RelayCommand BackCommand { get; }

    public RelayCommand OpenProjectCommand { get; }

    public RelayCommand RelinkProjectCommand { get; }

    public RelayCommand RevealProjectCommand { get; }

    public RelayCommand EditDescriptionCommand { get; }

    public RelayCommand SaveDescriptionCommand { get; }

    public RelayCommand CancelDescriptionCommand { get; }

    public RelayCommand EditTagsCommand { get; }

    public RelayCommand SaveTagsCommand { get; }

    public RelayCommand CancelTagsCommand { get; }

    public RelayCommand AddFeatureCommand { get; }

    public RelayCommand SaveFeatureCommand { get; }

    public RelayCommand CancelFeatureCommand { get; }

    public RelayCommand SaveMediaDescriptionCommand { get; }

    public RelayCommand CancelMediaCommand { get; }

    public bool IsEditingDescription
    {
        get => _isEditingDescription;
        private set => SetProperty(ref _isEditingDescription, value);
    }

    public string DescriptionDraft
    {
        get => _descriptionDraft;
        set => SetProperty(ref _descriptionDraft, value);
    }

    public bool IsEditingTags
    {
        get => _isEditingTags;
        private set => SetProperty(ref _isEditingTags, value);
    }

    public string TagsDraft
    {
        get => _tagsDraft;
        set => SetProperty(ref _tagsDraft, value);
    }

    public bool IsEditingFeature
    {
        get => _isEditingFeature;
        private set
        {
            if (SetProperty(ref _isEditingFeature, value))
            {
                RaiseFeatureViewProperties();
            }
        }
    }

    public bool ShowFeatureList => Project.HasFeatures && !IsEditingFeature;

    public bool ShowFeatureEmptyState => !Project.HasFeatures && !IsEditingFeature;

    public string FeatureEditorTitle => _editingFeature is null
        ? "新建功能"
        : $"编辑功能「{_editingFeature.Name}」";

    public string FeatureNameDraft
    {
        get => _featureNameDraft;
        set => SetProperty(ref _featureNameDraft, value);
    }

    public string FeatureDescriptionDraft
    {
        get => _featureDescriptionDraft;
        set => SetProperty(ref _featureDescriptionDraft, value);
    }

    public ObservableCollection<FeatureMediaSelectionViewModel> FeatureMediaSelections { get; } = new();

    public bool IsEditingMedia => _editingMedia is not null;

    public string EditingMediaTitle => _editingMedia is null
        ? string.Empty
        : $"素材说明 · {_editingMedia.FileName}";

    public string EditingMediaFeatureText => _editingMedia?.HasFeature == true
        ? $"关联功能：{_editingMedia.FeatureName}"
        : "未关联功能，可在功能模块中勾选关联";

    public string MediaDescriptionDraft
    {
        get => _mediaDescriptionDraft;
        set => SetProperty(ref _mediaDescriptionDraft, value);
    }

    private void RevealProject()
    {
        var projectPath = Project.ProjectPath;
        if (!Directory.Exists(projectPath))
        {
            _showStatus($"工程路径不存在：{projectPath}");
            return;
        }

        var assetsPath = Path.Combine(projectPath, "Assets");
        if (!Directory.Exists(assetsPath))
        {
            _showStatus("未找到工程的 Assets 文件夹，请确认工程结构完整。");
            return;
        }

        if (!_folderRevealService.Reveal(assetsPath))
        {
            _showStatus("无法打开资源管理器，请检查系统文件关联。");
            return;
        }

        _showStatus($"已在资源管理器中打开 Assets 文件夹：{assetsPath}");
    }

    private void RelinkProject()
    {
        var projectPath = _projectPathPicker.PickProjectDirectory();
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        if (!Project.RelinkProject(projectPath))
        {
            _showStatus("所选目录不是有效的 Unity 工程，需要包含 Assets、Packages 与 ProjectSettings。");
            return;
        }

        _showStatus("工程路径已重新关联。建议重新扫描以刷新素材索引。");
    }

    private async Task LaunchProjectAsync()
    {
        if (_isLaunching)
        {
            return;
        }

        _isLaunching = true;
        try
        {
            var result = await _projectLaunchService.LaunchAsync(
                Project.Name,
                Project.ProjectPath,
                Project.UnityVersion);

            if (result.Launched)
            {
                Project.MarkOpened(result.OpenedAt ?? DateTimeOffset.Now);
            }

            _showStatus(result.Message);
        }
        catch (Exception exception)
        {
            _showStatus($"启动失败：{exception.Message}");
        }
        finally
        {
            _isLaunching = false;
        }
    }

    public void BeginEditFeature(ProjectFeatureViewModel feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        OpenFeatureEditor(feature);
    }

    public void BeginEditMedia(ProjectMediaItemViewModel media)
    {
        ArgumentNullException.ThrowIfNull(media);

        CloseFeatureEditor();
        _editingMedia = media;
        MediaDescriptionDraft = media.Description;
        RaiseMediaEditorProperties();
    }

    public void DeleteFeature(ProjectFeatureViewModel feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        Project.RemoveFeature(feature);
        if (ReferenceEquals(_editingFeature, feature))
        {
            CloseFeatureEditor();
        }

        RaiseFeatureViewProperties();
        _showStatus($"功能「{feature.Name}」已删除。");
    }

    private void BeginEditDescription()
    {
        DescriptionDraft = Project.Description;
        IsEditingDescription = true;
    }

    private void SaveDescription()
    {
        Project.SetDescription(DescriptionDraft);
        IsEditingDescription = false;
        _showStatus("项目简介已更新。");
    }

    private void CancelDescription()
    {
        IsEditingDescription = false;
    }

    private void BeginEditTags()
    {
        TagsDraft = string.Join(", ", Project.Tags);
        IsEditingTags = true;
    }

    private void SaveTags()
    {
        var tags = TagsDraft
            .Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0);

        Project.SetTags(tags);
        IsEditingTags = false;
        _showStatus("项目标签已更新。");
    }

    private void CancelTags()
    {
        IsEditingTags = false;
    }

    private void BeginAddFeature()
    {
        OpenFeatureEditor(null);
    }

    private void OpenFeatureEditor(ProjectFeatureViewModel? feature)
    {
        CloseMediaEditor();

        _editingFeature = feature;
        FeatureNameDraft = feature?.Name ?? string.Empty;
        FeatureDescriptionDraft = feature?.Description ?? string.Empty;
        FeatureMediaSelections.Clear();

        foreach (var media in Project.AllMedia)
        {
            FeatureMediaSelections.Add(new FeatureMediaSelectionViewModel(
                media,
                feature is not null && ReferenceEquals(media.Feature, feature)));
        }

        IsEditingFeature = true;
        OnPropertyChanged(nameof(FeatureEditorTitle));
    }

    private void SaveFeature()
    {
        var name = FeatureNameDraft.Trim();
        if (name.Length == 0)
        {
            _showStatus("功能名称不能为空。");
            return;
        }

        var description = FeatureDescriptionDraft.Trim();
        ProjectFeatureViewModel feature;
        if (_editingFeature is null)
        {
            feature = Project.CreateFeature(name, description);
        }
        else
        {
            feature = _editingFeature;
            feature.Name = name;
            feature.Description = description;
        }

        Project.AssignMediaToFeature(
            feature,
            FeatureMediaSelections
                .Where(selection => selection.IsSelected)
                .Select(selection => selection.Media));

        CloseFeatureEditor();
        RaiseFeatureViewProperties();
        _showStatus($"功能「{feature.Name}」已保存。");
    }

    private void CancelFeature()
    {
        CloseFeatureEditor();
    }

    private void CloseFeatureEditor()
    {
        _editingFeature = null;
        FeatureMediaSelections.Clear();
        IsEditingFeature = false;
        FeatureNameDraft = string.Empty;
        FeatureDescriptionDraft = string.Empty;
        OnPropertyChanged(nameof(FeatureEditorTitle));
    }

    private void SaveMediaDescription()
    {
        if (_editingMedia is null)
        {
            return;
        }

        var mediaName = _editingMedia.FileName;
        Project.SetMediaDescription(_editingMedia, MediaDescriptionDraft);
        CloseMediaEditor();
        _showStatus($"素材「{mediaName}」的说明已更新。");
    }

    private void CancelMedia()
    {
        CloseMediaEditor();
    }

    private void CloseMediaEditor()
    {
        _editingMedia = null;
        MediaDescriptionDraft = string.Empty;
        RaiseMediaEditorProperties();
    }

    private void RaiseMediaEditorProperties()
    {
        OnPropertyChanged(nameof(IsEditingMedia));
        OnPropertyChanged(nameof(EditingMediaTitle));
        OnPropertyChanged(nameof(EditingMediaFeatureText));
    }

    private void RaiseFeatureViewProperties()
    {
        OnPropertyChanged(nameof(ShowFeatureList));
        OnPropertyChanged(nameof(ShowFeatureEmptyState));
    }
}
