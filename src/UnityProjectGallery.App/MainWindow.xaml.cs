using System.ComponentModel;
using System.Windows;
using UnityProjectGallery.App.ViewModels;

namespace UnityProjectGallery.App;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _flushInProgress;

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += OnWindowStateChanged;
        Loaded += (_, _) => UpdateMaximizeGlyph();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_flushInProgress)
        {
            return;
        }

        _flushInProgress = true;
        _ = FlushPendingSavesAndCloseAsync();
    }

    private async Task FlushPendingSavesAndCloseAsync()
    {
        try
        {
            if (DataContext is MainViewModel viewModel)
            {
                // 退出前最多等待 5 秒，避免数据库异常时窗口无法关闭。
                await viewModel.FlushPendingSavesAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (TimeoutException)
        {
        }
        finally
        {
            _allowClose = true;

            // 保存可能同步完成；不能在上一次 OnClosing 的调用栈内重入 Close()，
            // 因此交给调度器在下一轮消息循环中关闭窗口。
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    private void OnMinimizeClicked(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClicked(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeGlyph();
    }

    private void UpdateMaximizeGlyph()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        MaximizeGlyph.Text = isMaximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = isMaximized ? "还原" : "最大化";
    }
}
