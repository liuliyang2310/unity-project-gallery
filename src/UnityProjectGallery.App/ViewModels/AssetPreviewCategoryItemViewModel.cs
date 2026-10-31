using UnityProjectGallery.Core.Mvvm;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>Assets 预览窗口顶部的类型筛选项。</summary>
public sealed class AssetPreviewCategoryItemViewModel : ObservableObject
{
    private int _count;

    public AssetPreviewCategoryItemViewModel(string name, AssetPreviewKind? kind)
    {
        Name = name;
        Kind = kind;
    }

    public string Name { get; }

    public AssetPreviewKind? Kind { get; }

    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value))
            {
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    public string DisplayText => Count > 0 ? $"{Name} {Count}" : Name;
}
