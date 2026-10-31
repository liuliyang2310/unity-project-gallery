using System.Collections.ObjectModel;
using UnityProjectGallery.Core.Mvvm;
using UnityProjectGallery.Models;

namespace UnityProjectGallery.App.ViewModels;

/// <summary>一个功能模块及其关联的展示素材。</summary>
public sealed class ProjectFeatureViewModel : ObservableObject
{
    private string _name;
    private string _description;

    public ProjectFeatureViewModel(string name, string description = "", string? id = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        _name = name;
        _description = description;
    }

    public string Id { get; }

    public string Name
    {
        get => _name;
        internal set
        {
            if (SetProperty(ref _name, value))
            {
                foreach (var media in Media)
                {
                    media.NotifyFeatureChanged();
                }
            }
        }
    }

    public string Description
    {
        get => _description;
        internal set
        {
            if (SetProperty(ref _description, value))
            {
                OnPropertyChanged(nameof(HasDescription));
            }
        }
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public ObservableCollection<ProjectMediaItemViewModel> Media { get; } = new();

    public int MediaCount => Media.Count;

    public bool HasMedia => MediaCount > 0;

    public string MediaSummary => MediaCount == 0
        ? "未关联素材"
        : $"{MediaCount} 项关联素材";

    internal void AddMedia(ProjectMediaItemViewModel media)
    {
        if (!Media.Contains(media))
        {
            Media.Add(media);
            RaiseMediaProperties();
        }
    }

    internal void RemoveMedia(ProjectMediaItemViewModel media)
    {
        if (Media.Remove(media))
        {
            RaiseMediaProperties();
        }
    }

    public ProjectFeature ToModel()
    {
        return new ProjectFeature
        {
            Id = Id,
            Name = Name,
            Description = Description
        };
    }

    private void RaiseMediaProperties()
    {
        OnPropertyChanged(nameof(MediaCount));
        OnPropertyChanged(nameof(HasMedia));
        OnPropertyChanged(nameof(MediaSummary));
    }
}
