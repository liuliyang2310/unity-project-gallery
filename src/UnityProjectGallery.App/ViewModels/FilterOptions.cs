namespace UnityProjectGallery.App.ViewModels;

/// <summary>左侧导航对应的工程过滤方式。</summary>
public enum FilterMode
{
    All,
    RecentOpened,
    RecentlyModified,
    Favorites
}

/// <summary>卡片网格的排序方式。</summary>
public enum SortMode
{
    LastOpened,
    LastModified,
    Name,
    UnityVersion
}

/// <summary>排序下拉框选项。</summary>
public sealed class SortOption
{
    public SortOption(string label, SortMode mode)
    {
        Label = label;
        Mode = mode;
    }

    public string Label { get; }

    public SortMode Mode { get; }
}
