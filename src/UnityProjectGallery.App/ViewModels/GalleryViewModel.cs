using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using UnityProjectGallery.Core.Mvvm;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>
/// 项目库主页面：负责搜索、分类、排序与卡片集合，并支持增量合并扫描结果。
/// </summary>
public sealed class GalleryViewModel : ObservableObject
{
    private readonly List<ProjectItemViewModel> _allProjects;
    private readonly RelayCommand _clearSearchCommand;
    private string _searchText = string.Empty;
    private FilterMode _filterMode = FilterMode.All;
    private SortOption _selectedSortOption;
    private CategoryItemViewModel _selectedCategory;
    private ScanViewModel? _scan;

    public GalleryViewModel(IEnumerable<ProjectItemViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        _allProjects = projects.ToList();
        _clearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        SortOptions = new[]
        {
            new SortOption("最近修改", SortMode.LastModified),
            new SortOption("最近打开", SortMode.LastOpened),
            new SortOption("名称 A-Z", SortMode.Name),
            new SortOption("Unity 版本", SortMode.UnityVersion)
        };
        _selectedSortOption = SortOptions[0];
        _selectedCategory = BuildCategories();
        Refresh();

        foreach (var project in _allProjects)
        {
            project.PropertyChanged += OnProjectPropertyChanged;
        }
    }

    /// <summary>工程集合发生变化（扫描新增或收藏状态变化）时触发。</summary>
    public event EventHandler? ProjectsChanged;

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();

    public ObservableCollection<CategoryItemViewModel> Categories { get; } = new();

    public IReadOnlyList<SortOption> SortOptions { get; }

    public IReadOnlyList<ProjectItemViewModel> AllProjects => _allProjects;

    public RelayCommand ClearSearchCommand => _clearSearchCommand;

    public ScanViewModel? Scan
    {
        get => _scan;
        internal set => SetProperty(ref _scan, value);
    }

    public int TotalCount => _allProjects.Count;

    public bool HasNoProjects => TotalCount == 0;

    public bool IsEmpty => Projects.Count == 0;

    public string EmptyTitle => HasNoProjects
        ? "还没有扫描到 Unity 工程"
        : "没有找到匹配的工程";

    public string EmptyDescription => HasNoProjects
        ? "点击右上角「添加扫描目录」，选择包含 Unity 工程的文件夹开始只读扫描。"
        : "尝试更换搜索关键词，或切换分类与收藏筛选。";

    public string ResultSummary => Projects.Count == TotalCount
        ? $"共 {TotalCount} 个工程"
        : $"找到 {Projects.Count} 个 · 共 {TotalCount} 个";

    public string HeaderTitle => _filterMode switch
    {
        FilterMode.All => "全部工程",
        FilterMode.RecentOpened => "最近打开",
        FilterMode.RecentlyModified => "最近修改",
        FilterMode.Favorites => "收藏项目",
        _ => "全部工程"
    };

    public string HeaderSubtitle => _filterMode switch
    {
        FilterMode.All => "浏览本地已扫描的 Unity 工程，快速查看封面、素材、版本与依赖信息",
        FilterMode.RecentOpened => "按最近一次在 Unity Editor 中打开的时间排列",
        FilterMode.RecentlyModified => "按工程目录最近一次修改的时间排列",
        FilterMode.Favorites => "汇总你标记为收藏的 Unity 工程",
        _ => string.Empty
    };

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                Refresh();
            }
        }
    }

    public FilterMode FilterMode
    {
        get => _filterMode;
        set
        {
            if (!SetProperty(ref _filterMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));

            var forcedSort = value switch
            {
                FilterMode.RecentOpened => SortMode.LastOpened,
                FilterMode.RecentlyModified => SortMode.LastModified,
                _ => (SortMode?)null
            };
            if (forcedSort is { } mode && _selectedSortOption.Mode != mode)
            {
                _selectedSortOption = SortOptions.First(option => option.Mode == mode);
                OnPropertyChanged(nameof(SelectedSortOption));
            }

            Refresh();
        }
    }

    public SortOption SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (value is not null && SetProperty(ref _selectedSortOption, value))
            {
                Refresh();
            }
        }
    }

    public CategoryItemViewModel SelectedCategory => _selectedCategory;

    /// <summary>
    /// 合并新扫描结果：路径相同则更新扫描字段与素材，否则新增卡片；分类与筛选状态尽量保留。
    /// </summary>
    public ProjectMergeResult MergeProjects(
        IEnumerable<ScannedProjectBundle> scannedBundles,
        Func<UnityProject, ProjectItemViewModel> projectFactory)
    {
        ArgumentNullException.ThrowIfNull(scannedBundles);
        ArgumentNullException.ThrowIfNull(projectFactory);

        var added = 0;
        var updated = 0;

        foreach (var bundle in scannedBundles)
        {
            var scanned = bundle.Project;
            var existing = _allProjects.FirstOrDefault(project =>
                PathsEqual(project.ProjectPath, scanned.ProjectPath));

            if (existing is not null)
            {
                existing.ApplyScanResult(scanned);
                existing.ApplyMediaResult(bundle.MediaResult);
                updated++;
                continue;
            }

            var item = projectFactory(scanned);
            item.PropertyChanged += OnProjectPropertyChanged;
            item.ApplyMediaResult(bundle.MediaResult);
            _allProjects.Add(item);
            added++;
        }

        if (added == 0 && updated == 0)
        {
            return new ProjectMergeResult(0, 0);
        }

        var selectedName = _selectedCategory.Name;
        var selectedWasAll = _selectedCategory.IsAll;
        _selectedCategory = BuildCategories(selectedName, selectedWasAll);
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(TotalCount));

        Refresh();
        ProjectsChanged?.Invoke(this, EventArgs.Empty);

        return new ProjectMergeResult(added, updated);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private CategoryItemViewModel BuildCategories(string? preferredName = null, bool preferAll = true)
    {
        Categories.Clear();

        var allCategory = new CategoryItemViewModel("全部", _allProjects.Count, true, SelectCategory);
        Categories.Add(allCategory);

        var tagGroups = _allProjects
            .SelectMany(project => project.Tags)
            .GroupBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new { Name = group.First(), Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture);

        foreach (var tag in tagGroups)
        {
            Categories.Add(new CategoryItemViewModel(tag.Name, tag.Count, false, SelectCategory));
        }

        var selected = Categories.FirstOrDefault(category =>
                !category.IsAll &&
                !preferAll &&
                preferredName is not null &&
                string.Equals(category.Name, preferredName, StringComparison.CurrentCultureIgnoreCase))
            ?? allCategory;

        foreach (var category in Categories)
        {
            category.IsSelected = ReferenceEquals(category, selected);
        }

        return selected;
    }

    private void SelectCategory(CategoryItemViewModel category)
    {
        if (!ReferenceEquals(category, _selectedCategory))
        {
            _selectedCategory.IsSelected = false;
            _selectedCategory = category;
            OnPropertyChanged(nameof(SelectedCategory));
            Refresh();
        }

        // 重复点击当前分类时也重新通知一次，避免 ToggleButton 的本地状态与筛选状态不一致。
        category.IsSelected = true;
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectItemViewModel.IsFavorite))
        {
            Refresh();
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.PropertyName == nameof(ProjectItemViewModel.Tags))
        {
            var selectedName = _selectedCategory.Name;
            var selectedWasAll = _selectedCategory.IsAll;
            _selectedCategory = BuildCategories(selectedName, selectedWasAll);
            OnPropertyChanged(nameof(SelectedCategory));
            Refresh();
            return;
        }

        if (e.PropertyName == nameof(ProjectItemViewModel.Description))
        {
            Refresh();
            return;
        }

        if (e.PropertyName == nameof(ProjectItemViewModel.LastOpenedTime))
        {
            Refresh();
            ProjectsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>重新计算搜索结果，用于从详情页返回后同步功能名称等编辑内容。</summary>
    public void RefreshResults() => Refresh();

    private void Refresh()
    {
        var query = _allProjects.Where(Matches);
        var ordered = _selectedSortOption.Mode switch
        {
            SortMode.LastOpened => query
                .OrderByDescending(project => project.LastOpenedTime ?? DateTimeOffset.MinValue)
                .ToList(),
            SortMode.LastModified => query
                .OrderByDescending(project => project.LastModifiedTime ?? DateTimeOffset.MinValue)
                .ToList(),
            SortMode.Name => query
                .OrderBy(project => project.Name, StringComparer.CurrentCulture)
                .ToList(),
            SortMode.UnityVersion => query
                .OrderByDescending(project => project.UnityVersion, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => query.ToList()
        };

        Projects.Clear();
        foreach (var project in ordered)
        {
            Projects.Add(project);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoProjects));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDescription));
        OnPropertyChanged(nameof(ResultSummary));
    }

    private bool Matches(ProjectItemViewModel project)
    {
        if (_filterMode == FilterMode.Favorites && !project.IsFavorite)
        {
            return false;
        }

        if (_filterMode == FilterMode.RecentOpened && project.LastOpenedTime is null)
        {
            return false;
        }

        if (_filterMode == FilterMode.RecentlyModified && project.LastModifiedTime is null)
        {
            return false;
        }

        if (!_selectedCategory.IsAll &&
            !project.Tags.Contains(_selectedCategory.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            return false;
        }

        var keyword = _searchText.Trim();
        if (keyword.Length == 0)
        {
            return true;
        }

        return project.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)
            || project.ProjectPath.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || project.Description.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)
            || project.Tags.Any(tag => tag.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
            || project.Features.Any(feature =>
                feature.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase));
    }
}

/// <summary>一次扫描合并的统计结果。</summary>
public sealed record ProjectMergeResult(int Added, int Updated);
