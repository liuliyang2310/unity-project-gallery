using System.Windows;
using System.Windows.Controls;
using UnityProjectGallery.App.ViewModels;

namespace UnityProjectGallery.App.Views;

public partial class ProjectDetailView : UserControl
{
    public ProjectDetailView()
    {
        InitializeComponent();
    }

    private void OnEditFeatureClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectDetailViewModel viewModel &&
            sender is FrameworkElement { Tag: ProjectFeatureViewModel feature })
        {
            viewModel.BeginEditFeature(feature);
        }
    }

    private void OnDeleteFeatureClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectDetailViewModel viewModel ||
            sender is not FrameworkElement { Tag: ProjectFeatureViewModel feature })
        {
            return;
        }

        var result = MessageBox.Show(
            $"确定删除功能「{feature.Name}」？\n关联关系会被清除，但不会删除任何素材。",
            "删除功能",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            viewModel.DeleteFeature(feature);
        }
    }

    private void OnEditMediaClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectDetailViewModel viewModel &&
            sender is FrameworkElement { Tag: ProjectMediaItemViewModel media })
        {
            viewModel.BeginEditMedia(media);
        }
    }
}
