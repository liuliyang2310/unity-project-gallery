using UnityProjectGallery.Core.Mvvm;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>功能编辑器中用于勾选关联素材的临时选择项。</summary>
public sealed class FeatureMediaSelectionViewModel : ObservableObject
{
    private bool _isSelected;

    public FeatureMediaSelectionViewModel(ProjectMediaItemViewModel media, bool isSelected)
    {
        Media = media;
        _isSelected = isSelected;
    }

    public ProjectMediaItemViewModel Media { get; }

    public string FileName => Media.FileName;

    public string MetadataText => Media.MetadataText;

    public string AssignmentText => Media.HasFeature
        ? $"当前关联：{Media.FeatureName}"
        : "未关联功能";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
