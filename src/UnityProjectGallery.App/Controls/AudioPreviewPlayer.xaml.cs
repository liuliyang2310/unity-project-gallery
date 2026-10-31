using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NAudio.Vorbis;
using NAudio.Wave;

namespace UnityProjectGallery.App.Controls;

/// <summary>
/// 简单音频播放控件：支持 WAV / MP3 / OGG / AIFF / FLAC / M4A，
/// 提供播放暂停、进度拖动与时长显示。
/// </summary>
public partial class AudioPreviewPlayer : UserControl
{
    public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register(
        nameof(SourcePath),
        typeof(string),
        typeof(AudioPreviewPlayer),
        new PropertyMetadata(null, OnSourcePathChanged));

    private readonly DispatcherTimer _positionTimer;
    private WaveStream? _reader;
    private WaveOutEvent? _output;
    private string? _loadedPath;
    private bool _isSeeking;
    private bool _isPlaying;

    public AudioPreviewPlayer()
    {
        InitializeComponent();

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _positionTimer.Tick += OnPositionTimerTick;
    }

    public string? SourcePath
    {
        get => (string?)GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    /// <summary>播放或暂停当前音频。</summary>
    public void TogglePlayback()
    {
        if (_reader is null || _output is null)
        {
            return;
        }

        try
        {
            if (_isPlaying)
            {
                _output.Pause();
                _isPlaying = false;
                _positionTimer.Stop();
            }
            else
            {
                if (_reader.CurrentTime >= _reader.TotalTime - TimeSpan.FromMilliseconds(100))
                {
                    _reader.CurrentTime = TimeSpan.Zero;
                }

                _output.Play();
                _isPlaying = true;
                _positionTimer.Start();
            }

            UpdatePlayGlyph();
        }
        catch (Exception exception)
        {
            ShowError($"无法播放该音频。{Environment.NewLine}{exception.Message}");
        }
    }

    /// <summary>停止播放并释放音频设备。</summary>
    public void StopPlayback()
    {
        DisposePlayback();
    }

    private static void OnSourcePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioPreviewPlayer player)
        {
            player.OpenAudio(e.NewValue as string);
        }
    }

    private void OpenAudio(string? filePath)
    {
        if (string.Equals(filePath, _loadedPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _loadedPath = filePath;
        DisposePlayback();
        ErrorPanel.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            FileText.Text = "音频预览";
            TypeText.Text = "选择音频文件后在此播放";
            return;
        }

        FileText.Text = Path.GetFileName(filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("音频文件不存在。", filePath);
            }

            _reader = CreateReader(filePath);
            if (_reader.TotalTime <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("音频文件没有可播放的时长。");
            }

            _output = new WaveOutEvent { DesiredLatency = 120 };
            _output.Init(_reader);
            _output.PlaybackStopped += OnPlaybackStopped;

            TypeText.Text = $"{Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant()} · " +
                            FormatTime(_reader.TotalTime);
            PositionSlider.Maximum = Math.Max(1, _reader.TotalTime.TotalSeconds);
            PositionSlider.Value = 0;
            PositionSlider.IsEnabled = true;
            PositionText.Text = "00:00";
            DurationText.Text = FormatTime(_reader.TotalTime);
            PlayButton.IsEnabled = true;
            ControlsPanel.Visibility = Visibility.Visible;
            UpdatePlayGlyph();
        }
        catch (Exception exception)
        {
            ShowError($"无法加载该音频，可能是格式不受支持或缺少解码器。" +
                      $"{Environment.NewLine}{exception.Message}");
        }
    }

    private static WaveStream CreateReader(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".ogg" => new VorbisWaveReader(filePath),
            ".aif" or ".aiff" => new AiffFileReader(filePath),
            ".wav" => new WaveFileReader(filePath),
            ".mp3" => new Mp3FileReader(filePath),
            ".flac" or ".m4a" => new MediaFoundationReader(filePath),
            _ => throw new NotSupportedException($"暂不支持 {Path.GetExtension(filePath)} 音频格式。")
        };
    }

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
    {
        TogglePlayback();
    }

    private void UpdatePlayGlyph()
    {
        PlayGlyph.Text = _isPlaying ? "\uE769" : "\uE768";
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (_reader is null)
        {
            return;
        }

        if (!_isSeeking)
        {
            PositionSlider.Value = Math.Clamp(
                _reader.CurrentTime.TotalSeconds,
                0,
                PositionSlider.Maximum);
            PositionText.Text = FormatTime(_reader.CurrentTime);
        }

        if (_output?.PlaybackState == PlaybackState.Stopped && _isPlaying)
        {
            _isPlaying = false;
            _positionTimer.Stop();
            UpdatePlayGlyph();
        }
    }

    private void OnPositionSliderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_reader is not null)
        {
            _isSeeking = true;
        }
    }

    private void OnPositionSliderMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_reader is not null)
        {
            _reader.CurrentTime = TimeSpan.FromSeconds(PositionSlider.Value);
            PositionText.Text = FormatTime(_reader.CurrentTime);
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

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (e.Exception is not null)
            {
                ShowError($"播放过程中发生错误。{Environment.NewLine}{e.Exception.Message}");
                return;
            }

            if (_output?.PlaybackState == PlaybackState.Stopped)
            {
                _isPlaying = false;
                _positionTimer.Stop();
                UpdatePlayGlyph();
            }
        }));
    }

    private void ShowError(string message)
    {
        DisposePlayback();
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
        ControlsPanel.Visibility = Visibility.Collapsed;
        TypeText.Text = "无法加载";
    }

    private void DisposePlayback()
    {
        _positionTimer.Stop();
        _isPlaying = false;

        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            try
            {
                _output.Stop();
            }
            catch (Exception)
            {
            }

            _output.Dispose();
            _output = null;
        }

        _reader?.Dispose();
        _reader = null;

        PlayButton.IsEnabled = false;
        PositionSlider.IsEnabled = false;
        PositionSlider.Maximum = 1;
        PositionSlider.Value = 0;
        PositionText.Text = "00:00";
        DurationText.Text = "00:00";
        UpdatePlayGlyph();
    }

    private void OnControlUnloaded(object sender, RoutedEventArgs e)
    {
        DisposePlayback();
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
