using UnityProjectGallery.Core.Mvvm;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>左侧主导航项目。</summary>
public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isActive;
    private int _count;

    public NavigationItemViewModel(
        string title,
        string glyph,
        FilterMode filterMode,
        Action<NavigationItemViewModel> select)
    {
        ArgumentNullException.ThrowIfNull(select);

        Title = title;
        Glyph = glyph;
        FilterMode = filterMode;
        Command = new RelayCommand(() => select(this));
    }

    public string Title { get; }

    public string Glyph { get; }

    public FilterMode FilterMode { get; }

    public RelayCommand Command { get; }

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            // 点击当前导航项时 ToggleButton 会先改变本地选中状态，
            // 这里始终通知一次，让单向绑定把视觉状态同步回 ViewModel。
            OnPropertyChanged();
        }
    }
}
