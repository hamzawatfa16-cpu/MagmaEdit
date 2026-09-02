using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MagmaEdit.Core.Media;
using Sprocket.Core.Timing;

namespace MagmaEdit.App;

/// <summary>Connects the existing preview surface to a persistent FFmpeg-backed playback session.</summary>
public sealed class PreviewPlaybackController : IAsyncDisposable
{
    private readonly Window _window;
    private readonly Border _previewCanvas;
    private readonly Button _playButton;
    private readonly Slider _timelineSlider;
    private readonly TextBlock _timeText;

    private MediaPlaybackSession? _session;
    private string? _path;
    private CancellationTokenSource? _playbackCts;
    private Task? _playbackTask;
    private CancellationTokenSource? _scrubCts;
    private Bitmap? _bitmap;
    private bool _updatingSlider;
    private bool _disposed;

    private PreviewPlaybackController(
        Window window,
        Border previewCanvas,
        Button playButton,
        Slider timelineSlider,
        TextBlock timeText)
    {
        _window = window;
        _previewCanvas = previewCanvas;
        _playButton = playButton;
        _timelineSlider = timelineSlider;
        _timeText = timeText;

        _playButton.Click += PlayButton_Click;
        _timelineSlider.ValueChanged += TimelineSlider_ValueChanged;
        _window.AddHandler(Button.ClickEvent, MediaButton_Click, RoutingStrategies.Bubble);
        _window.Closed += Window_Closed;
    }

    /// <summary>Attaches playback controls to the main window's existing 9:16 preview region.</summary>
    public static PreviewPlaybackController Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.Content is not Grid root || root.Children.Count < 3)
        {
            throw new InvalidOperationException("MagmaEdit preview layout is not available.");
        }

        if (root.Children[2] is not Border previewBorder ||
            previewBorder.Child is not Border previewCanvas)
        {
            throw new InvalidOperationException("MagmaEdit 9:16 preview surface is not available.");
        }

        var playButton = new Button { Content = "Play", Width = 72 };
        var timeText = new TextBlock
        {
            Text = "0.00 / 0.00 s",
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.75
        };
        var timelineSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Width = 360,
            HorizontalAlignment = HorizontalAlignment.Center,
            SmallChange = 0.1,
            LargeChange = 1
        };

        var controls = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { playButton, timeText }
                },
                timelineSlider
            }
        };

        previewBorder.Child = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { previewCanvas, controls }
        };

        return new PreviewPlaybackController(window, previewCanvas, playButton, timelineSlider, timeText);
    }

    private async void MediaButton_Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button || !button.IsEffectivelyVisible)
        {
            return;
        }

        string? path = ToolTip.GetTip(button)?.ToString();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        await SwitchMediaAsync(path);
    }

    private async Task SwitchMediaAsync(string path)
    {
        CancellationTokenSource? previousScrub = Interlocked.Exchange(ref _scrubCts, null);
        previousScrub?.Cancel();
        previousScrub?.Dispose();

        await StopPlaybackAsync();
        await DisposeSessionAsync();

        if (_disposed)
        {
            return;
        }

        try
        {
            var session = new MediaPlaybackSession(path);
            _session = session;
            _path = path;

            ConfigureDuration(session.Info.Duration);
            DecodedPreviewFrame? frame = await session.ReadNextFrameAsync();
            if (frame is null || _disposed || !string.Equals(_path, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplyPreviewFrame(frame);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or
            InvalidDataException or
            IOException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            ShowError($"Preview unavailable\n{exception.Message}");
        }
    }

    private async void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        if (_playbackTask is { IsCompleted: false })
        {
            await StopPlaybackAsync();
            return;
        }

        StartPlayback();
    }

    private void StartPlayback()
    {
        if (_session is null || _playbackTask is { IsCompleted: false })
        {
            return;
        }

        _playbackCts?.Cancel();
        _playbackCts?.Dispose();
        _playbackCts = new CancellationTokenSource();
        _playButton.Content = "Pause";
        _playbackTask = PlaybackLoopAsync(_session, _playbackCts.Token);
    }

    private async Task PlaybackLoopAsync(MediaPlaybackSession session, CancellationToken cancellationToken)
    {
        double fps = session.Info.FrameRate.Num > 0 && session.Info.FrameRate.Den > 0
            ? (double)session.Info.FrameRate.Num / session.Info.FrameRate.Den
            : 30.0;
        TimeSpan frameDelay = TimeSpan.FromSeconds(1.0 / Math.Clamp(fps, 1.0, 240.0));
        bool reachedEnd = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                DecodedPreviewFrame? frame = await session.ReadNextFrameAsync(cancellationToken).ConfigureAwait(false);
                if (frame is null)
                {
                    reachedEnd = true;
                    break;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (!_disposed && ReferenceEquals(_session, session))
                    {
                        ApplyPreviewFrame(frame);
                    }
                });

                await Task.Delay(frameDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed || !ReferenceEquals(_session, session))
                {
                    return;
                }

                _playButton.Content = "Play";
                if (reachedEnd)
                {
                    _updatingSlider = true;
                    try
                    {
                        _timelineSlider.Value = _timelineSlider.Maximum;
                        UpdateTimeText(_timelineSlider.Maximum);
                    }
                    finally
                    {
                        _updatingSlider = false;
                    }
                }
            });
        }
    }

    private async void TimelineSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_updatingSlider || _session is null || _timelineSlider.Maximum <= 0)
        {
            return;
        }

        double seconds = Math.Clamp(e.NewValue, 0, _timelineSlider.Maximum);
        UpdateTimeText(seconds);

        CancellationTokenSource? previous = Interlocked.Exchange(ref _scrubCts, new CancellationTokenSource());
        previous?.Cancel();
        previous?.Dispose();

        CancellationTokenSource? scrub = _scrubCts;
        MediaPlaybackSession? session = _session;
        if (scrub is null || session is null)
        {
            return;
        }

        try
        {
            DecodedPreviewFrame? frame = await session
                .SeekAndReadFrameAsync(Timecode.FromSeconds(seconds), scrub.Token)
                .ConfigureAwait(false);
            if (frame is null || scrub.IsCancellationRequested)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (!_disposed && ReferenceEquals(_session, session))
                {
                    ApplyPreviewFrame(frame);
                }
            });
        }
        catch (OperationCanceledException) when (scrub.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ApplyPreviewFrame(DecodedPreviewFrame frame)
    {
        WriteableBitmap bitmap = CreatePreviewBitmap(frame);
        Bitmap? previous = _bitmap;
        _bitmap = bitmap;
        _previewCanvas.Background = new ImageBrush(bitmap) { Stretch = Stretch.Uniform };
        if (_previewCanvas.Child is TextBlock statusText)
        {
            statusText.IsVisible = false;
        }
        previous?.Dispose();

        double seconds = Math.Clamp(frame.Pts.ToSeconds(), 0, _timelineSlider.Maximum);
        _updatingSlider = true;
        try
        {
            _timelineSlider.Value = seconds;
        }
        finally
        {
            _updatingSlider = false;
        }

        UpdateTimeText(seconds);
    }

    private void ConfigureDuration(Timecode duration)
    {
        double seconds = Math.Max(0, duration.ToSeconds());
        _updatingSlider = true;
        try
        {
            _timelineSlider.Maximum = Math.Max(seconds, 0.001);
            _timelineSlider.Value = 0;
        }
        finally
        {
            _updatingSlider = false;
        }

        UpdateTimeText(0);
        _playButton.Content = "Play";
    }

    private void UpdateTimeText(double seconds)
    {
        _timeText.Text = $"{seconds:0.00} / {_timelineSlider.Maximum:0.00} s";
    }

    private void ShowError(string message)
    {
        _previewCanvas.Background = Brushes.Black;
        _playButton.Content = "Play";
        _timeText.Text = "0.00 / 0.00 s";
        if (_previewCanvas.Child is TextBlock statusText)
        {
            statusText.Text = message;
            statusText.IsVisible = true;
            return;
        }

        _previewCanvas.Child = new TextBlock
        {
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.75
        };
    }

    private static WriteableBitmap CreatePreviewBitmap(DecodedPreviewFrame frame)
    {
        GCHandle handle = GCHandle.Alloc(frame.Pixels, GCHandleType.Pinned);
        try
        {
            return new WriteableBitmap(
                PixelFormats.Rgba8888,
                AlphaFormat.Opaque,
                handle.AddrOfPinnedObject(),
                new PixelSize(frame.Width, frame.Height),
                new Vector(96, 96),
                frame.RowBytes);
        }
        finally
        {
            handle.Free();
        }
    }

    private async Task StopPlaybackAsync()
    {
        CancellationTokenSource? cts = Interlocked.Exchange(ref _playbackCts, null);
        cts?.Cancel();

        Task? task = Interlocked.Exchange(ref _playbackTask, null);
        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts?.Dispose();
        _playButton.Content = "Play";
    }

    private async Task DisposeSessionAsync()
    {
        MediaPlaybackSession? session = Interlocked.Exchange(ref _session, null);
        _path = null;
        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.RemoveHandler(Button.ClickEvent, MediaButton_Click);
        _playButton.Click -= PlayButton_Click;
        _timelineSlider.ValueChanged -= TimelineSlider_ValueChanged;
        _window.Closed -= Window_Closed;

        CancellationTokenSource? scrub = Interlocked.Exchange(ref _scrubCts, null);
        scrub?.Cancel();
        scrub?.Dispose();

        CancellationTokenSource? playback = Interlocked.Exchange(ref _playbackCts, null);
        playback?.Cancel();

        Task? task = Interlocked.Exchange(ref _playbackTask, null);
        if (task is not null)
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        playback?.Dispose();
        MediaPlaybackSession? session = Interlocked.Exchange(ref _session, null);
        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        _bitmap?.Dispose();
        _bitmap = null;
    }
}
