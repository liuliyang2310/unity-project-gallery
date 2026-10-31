using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace UnityProjectGallery.App.Views;

/// <summary>图片大图与视频播放窗口。支持 F11 / Esc 全屏切换。</summary>
public partial class MediaViewerWindow : Window
{
    private readonly string _filePath;
    private readonly bool _isVideoMode;
    private readonly DispatcherTimer _positionTimer;
    private bool _isPlaying;
    private bool _isSeeking;
    private bool _isFullScreen;

    public MediaViewerWindow(string filePath, string title, bool isVideo)
    {
        InitializeComponent();

        _filePath = filePath;
        _isVideoMode = isVideo;
        Title = title;
        TitleText.Text = title;
        TypeGlyph.Text = isVideo ? "\uE714" : "\uE91B";
        MetaText.Text = isVideo ? "视频" : "图片";

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _positionTimer.Tick += OnPositionTimerTick;

        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();

        if (_isVideoMode)
        {
            ImageViewer.Visibility = Visibility.Collapsed;
            Player.Visibility = Visibility.Visible;
            VideoControls.Visibility = Visibility.Visible;
            PositionSlider.IsEnabled = false;
            Player.Source = new Uri(_filePath, UriKind.Absolute);
            Player.Play();
            return;
        }

        LoadImage();
    }

    private void LoadImage()
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(_filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            ImageViewer.Source = bitmap;
            ImageViewer.Visibility = Visibility.Visible;
            MetaText.Text = $"图片 · {bitmap.PixelWidth} × {bitmap.PixelHeight}";
        }
        catch (Exception)
        {
            ImageViewer.Visibility = Visibility.Collapsed;
            ShowMessage("无法打开该图片。文件可能已损坏，或缺少对应的图片编码器。");
        }
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        var duration = Player.NaturalDuration.HasTimeSpan
            ? Player.NaturalDuration.TimeSpan
            : TimeSpan.Zero;

        PositionSlider.Maximum = Math.Max(1, duration.TotalSeconds);
        PositionSlider.IsEnabled = duration > TimeSpan.Zero;
        DurationText.Text = FormatTime(duration);
        MetaText.Text = duration > TimeSpan.Zero
            ? $"视频 · {FormatTime(duration)}"
            : "视频";

        Player.Play();
        _isPlaying = true;
        UpdatePlayGlyph();
        _positionTimer.Start();
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _isPlaying = false;
        _positionTimer.Stop();
        UpdatePlayGlyph();
        PlayPauseButton.IsEnabled = false;
        PositionSlider.IsEnabled = false;
        ShowMessage(
            "无法播放该视频。文件可能已损坏，或系统缺少此格式所需的解码器。" +
            Environment.NewLine +
            e.ErrorException.Message);
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        _isPlaying = false;
        _positionTimer.Stop();
        UpdatePlayGlyph();
        PositionSlider.Value = 0;
        PositionText.Text = "00:00";
    }

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
    {
        if (!_isVideoMode)
        {
            return;
        }

        if (_isPlaying)
        {
            Player.Pause();
            _isPlaying = false;
        }
        else
        {
            if (Player.NaturalDuration.HasTimeSpan &&
                PositionSlider.Maximum > 0 &&
                Player.Position >= Player.NaturalDuration.TimeSpan)
            {
                Player.Position = TimeSpan.Zero;
            }

            Player.Play();
            _isPlaying = true;
            if (!_positionTimer.IsEnabled)
            {
                _positionTimer.Start();
            }
        }

        UpdatePlayGlyph();
    }

    private void UpdatePlayGlyph()
    {
        PlayGlyph.Text = _isPlaying ? "\uE769" : "\uE768";
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (_isSeeking || !Player.NaturalDuration.HasTimeSpan)
        {
            return;
        }

        var duration = Player.NaturalDuration.TimeSpan;
        var position = Player.Position;
        PositionSlider.Value = Math.Clamp(position.TotalSeconds, 0, PositionSlider.Maximum);
        PositionText.Text = FormatTime(position);
        DurationText.Text = FormatTime(duration);
    }

    private void OnPositionSliderMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isSeeking = true;
    }

    private void OnPositionSliderMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (Player.NaturalDuration.HasTimeSpan)
        {
            var target = TimeSpan.FromSeconds(PositionSlider.Value);
            Player.Position = target;
            PositionText.Text = FormatTime(target);
        }

        _isSeeking = false;
    }

    private void OnPositionSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSeeking)
        {
            PositionText.Text = FormatTime(TimeSpan.FromSeconds(e.NewValue));
        }
    }

    private void OnToggleFullscreenClicked(object sender, RoutedEventArgs e)
    {
        _isFullScreen = !_isFullScreen;
        TitleBar.Visibility = _isFullScreen ? Visibility.Collapsed : Visibility.Visible;
        WindowState = _isFullScreen ? WindowState.Maximized : WindowState.Normal;
        FullscreenGlyph.Text = _isFullScreen ? "\uE73F" : "\uE740";
        VideoFullscreenGlyph.Text = _isFullScreen ? "\uE73F" : "\uE740";
    }

    private void ExitFullScreen()
    {
        if (!_isFullScreen)
        {
            return;
        }

        _isFullScreen = false;
        TitleBar.Visibility = Visibility.Visible;
        WindowState = WindowState.Normal;
        FullscreenGlyph.Text = "\uE740";
        VideoFullscreenGlyph.Text = "\uE740";
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                if (_isFullScreen)
                {
                    ExitFullScreen();
                }
                else
                {
                    Close();
                }

                e.Handled = true;
                break;
            case Key.F11:
                OnToggleFullscreenClicked(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Space when _isVideoMode:
                OnPlayPauseClicked(this, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    private void ShowMessage(string message)
    {
        MessageText.Text = message;
        MessageOverlay.Visibility = Visibility.Visible;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _positionTimer.Stop();
        try
        {
            Player.Stop();
            Player.Close();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time <= TimeSpan.Zero)
        {
            return "00:00";
        }

        return time.TotalHours >= 1
            ? time.ToString(@"hh\:mm\:ss")
            : time.ToString(@"mm\:ss");
    }
}
