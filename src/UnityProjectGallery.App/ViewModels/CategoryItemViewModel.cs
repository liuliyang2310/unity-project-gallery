using UnityProjectGallery.Core.Mvvm;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>标签分类筛选项，侧边栏与筛选条共享同一份实例。</summary>
public sealed class CategoryItemViewModel : ObservableObject
{
    private bool _isSelected;

    public CategoryItemViewModel(
        string name,
        int count,
        bool isAll,
        Action<CategoryItemViewModel> select)
    {
        ArgumentNullException.ThrowIfNull(select);

        Name = name;
        Count = count;
        IsAll = isAll;
        Command = new RelayCommand(() => select(this));
    }

    public string Name { get; }

    public int Count { get; }

    public bool IsAll { get; }

    public RelayCommand Command { get; }

    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            _isSelected = value;
            // 重复点击当前分类时也需要刷新单选按钮的视觉状态。
            OnPropertyChanged();
        }
    }
}
