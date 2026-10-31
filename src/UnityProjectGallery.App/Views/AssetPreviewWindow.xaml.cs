using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UnityProjectGallery.App.ViewModels;

namespace UnityProjectGallery.App.Views;

/// <summary>工程 Assets 中的模型、图片与音频预览窗口。</summary>
public partial class AssetPreviewWindow : Window
{
    public AssetPreviewWindow()
    {
        InitializeComponent();
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (DataContext is AssetPreviewViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        AudioPlayer.StopPlayback();
        if (DataContext is AssetPreviewViewModel viewModel)
        {
            viewModel.Dispose();
        }

        base.OnClosed(e);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnResetCameraClicked(object sender, RoutedEventArgs e)
    {
        ModelView.ResetCamera();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var isTextBoxFocused = Keyboard.FocusedElement is TextBox;

        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.R when !isTextBoxFocused:
                if (DataContext is AssetPreviewViewModel { IsModelSelected: true })
                {
                    ModelView.ResetCamera();
                    e.Handled = true;
                }

                break;
            case Key.Space when !isTextBoxFocused:
                if (DataContext is AssetPreviewViewModel { IsAudioSelected: true })
                {
                    AudioPlayer.TogglePlayback();
                    e.Handled = true;
                }

                break;
        }
    }
}
