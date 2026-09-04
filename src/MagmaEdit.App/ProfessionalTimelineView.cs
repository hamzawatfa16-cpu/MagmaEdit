using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.App;

/// <summary>Provides the first professional timeline surface without bypassing the existing editor command layer.</summary>
internal sealed class ProfessionalTimelineView : UserControl
{
    private const double TrackLabelWidth = 128;
    private const double TrackHeight = 54;
    private const double RulerHeight = 34;
    private const double MinimumPixelsPerSecond = 40;
    private const double MaximumPixelsPerSecond = 220;
    private const double DefaultPixelsPerSecond = 80;

    private readonly Func<ProjectDocument> _projectProvider;
    private readonly Action<string> _statusReporter;
    private readonly Canvas _timelineCanvas;
    private readonly Slider _zoomSlider;
    private readonly CheckBox _snapCheckBox;
    private readonly TextBlock _zoomText;
    private readonly TextBlock _playheadText;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Border _playhead;

    private string? _selectedTrackId;
    private string? _selectedClipId;
    private double _playheadSeconds;
    private string _renderSignature = string.Empty;

    public ProfessionalTimelineView(Func<ProjectDocument> projectProvider, Action<string> statusReporter)
    {
        ArgumentNullException.ThrowIfNull(projectProvider);
        ArgumentNullException.ThrowIfNull(statusReporter);

        _projectProvider = projectProvider;
        _statusReporter = statusReporter;
        _timelineCanvas = new Canvas
        {
            Background = Brushes.Transparent
        };
        _zoomSlider = new Slider
        {
            Minimum = MinimumPixelsPerSecond,
            Maximum = MaximumPixelsPerSecond,
            Value = DefaultPixelsPerSecond,
            Width = 150
        };
        _snapCheckBox = new CheckBox
        {
            Content = "Snap 1s",
            IsChecked = true
        };
        _zoomText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.75
        };
        _playheadText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.75
        };
        _playhead = new Border
        {
            Width = 2,
            Background = Brushes.Red,
            IsHitTestVisible = false
        };
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        _zoomSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property == Slider.ValueProperty)
            {
                UpdateToolbar();
                RenderTimeline();
            }
        };
        _snapCheckBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == ToggleButton.IsCheckedProperty)
            {
                UpdatePlayhead(_playheadSeconds);
            }
        };
        _timelineCanvas.PointerPressed += TimelineCanvas_PointerPressed;
        _refreshTimer.Tick += (_, _) => RefreshIfProjectChanged();
        Unloaded += (_, _) => _refreshTimer.Stop();
        Loaded += (_, _) =>
        {
            _refreshTimer.Start();
            RenderTimeline();
        };

        Content = BuildLayout();
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 0, 0, 8)
        };
        toolbar.Children.Add(new TextBlock
        {
            Text = "Timeline",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        toolbar.Children.Add(_zoomText);
        toolbar.Children.Add(_zoomSlider);
        toolbar.Children.Add(_snapCheckBox);
        toolbar.Children.Add(_playheadText);
        toolbar.Children.Add(new TextBlock
        {
            Text = "Click the ruler or lane to move the playhead. Clips are selected, not dragged between tracks.",
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.65
        });

        root.Children.Add(toolbar);
        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _timelineCanvas
        };
        Grid.SetRow(scrollViewer, 1);
        root.Children.Add(scrollViewer);
        return root;
    }

    private void RefreshIfProjectChanged()
    {
        ProjectDocument project = _projectProvider();
        string signature = BuildRenderSignature(project);
        if (!string.Equals(signature, _renderSignature, StringComparison.Ordinal))
        {
            RenderTimeline();
        }
    }

    private void RenderTimeline()
    {
        ProjectDocument project = _projectProvider();
        _timelineCanvas.Children.Clear();

        double pixelsPerSecond = _zoomSlider.Value;
        double maxSeconds = Math.Max(
            10,
            project.Timeline.Tracks
                .SelectMany(track => track.Clips)
                .Select(clip => clip.TimelineEnd.ToSeconds())
                .DefaultIfEmpty(0)
                .Max() + 5);
        double timelineWidth = TrackLabelWidth + maxSeconds * pixelsPerSecond;
        double timelineHeight = RulerHeight + Math.Max(1, project.Timeline.Tracks.Count) * TrackHeight;
        _timelineCanvas.Width = timelineWidth;
        _timelineCanvas.Height = timelineHeight;

        RenderRuler(maxSeconds, pixelsPerSecond, timelineWidth);

        for (int index = 0; index < project.Timeline.Tracks.Count; index++)
        {
            TimelineTrack track = project.Timeline.Tracks[index];
            double top = RulerHeight + index * TrackHeight;
            RenderTrack(track, top, pixelsPerSecond, timelineWidth);
        }

        if (project.Timeline.Tracks.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No tracks",
                Margin = new Thickness(12)
            };
            Canvas.SetLeft(empty, TrackLabelWidth + 12);
            Canvas.SetTop(empty, RulerHeight + 16);
            _timelineCanvas.Children.Add(empty);
        }

        _timelineCanvas.Children.Add(_playhead);
        UpdatePlayhead(_playheadSeconds);
        UpdateToolbar();
        _renderSignature = BuildRenderSignature(project);
    }

    private void RenderRuler(double maxSeconds, double pixelsPerSecond, double timelineWidth)
    {
        var rulerBackground = new Border
        {
            Width = timelineWidth,
            Height = RulerHeight,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Brushes.Gray
        };
        _timelineCanvas.Children.Add(rulerBackground);

        for (int second = 0; second <= Math.Ceiling(maxSeconds); second++)
        {
            double left = TrackLabelWidth + second * pixelsPerSecond;
            var tick = new Border
            {
                Width = 1,
                Height = second % 5 == 0 ? 22 : 12,
                Background = Brushes.Gray
            };
            Canvas.SetLeft(tick, left);
            Canvas.SetTop(tick, RulerHeight - tick.Height);
            _timelineCanvas.Children.Add(tick);

            if (second % 5 == 0)
            {
                var label = new TextBlock
                {
                    Text = FormatSeconds(second),
                    FontSize = 11,
                    Opacity = 0.75
                };
                Canvas.SetLeft(label, left + 4);
                Canvas.SetTop(label, 4);
                _timelineCanvas.Children.Add(label);
            }
        }

        var rulerLabel = new TextBlock
        {
            Text = "TRACKS",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.65
        };
        Canvas.SetLeft(rulerLabel, 8);
        Canvas.SetTop(rulerLabel, 9);
        _timelineCanvas.Children.Add(rulerLabel);
    }

    private void RenderTrack(TimelineTrack track, double top, double pixelsPerSecond, double timelineWidth)
    {
        var row = new Border
        {
            Width = timelineWidth,
            Height = TrackHeight - 1,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Brushes.Gray
        };
        Canvas.SetLeft(row, 0);
        Canvas.SetTop(row, top);
        _timelineCanvas.Children.Add(row);

        var trackLabel = new Button
        {
            Content = track.Name,
            Width = TrackLabelWidth - 8,
            Height = TrackHeight - 8,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4),
            Background = string.Equals(track.Id, _selectedTrackId, StringComparison.Ordinal)
                ? Brushes.DodgerBlue
                : null
        };
        trackLabel.Click += (_, _) => SelectTrack(track);
        Canvas.SetLeft(trackLabel, 0);
        Canvas.SetTop(trackLabel, top);
        _timelineCanvas.Children.Add(trackLabel);

        foreach (TimelineClip clip in track.Clips)
        {
            MediaAsset? media = _projectProvider().Media.FirstOrDefault(asset =>
                string.Equals(asset.Id, clip.MediaId, StringComparison.Ordinal));
            string mediaName = media?.FileName ?? $"Missing media {clip.MediaId}";
            double left = TrackLabelWidth + clip.TimelineStart.ToSeconds() * pixelsPerSecond;
            double width = Math.Max(36, clip.Duration.ToSeconds() * pixelsPerSecond);

            var clipButton = new Button
            {
                Content = new TextBlock
                {
                    Text = $"{mediaName}\n{clip.Duration.ToSeconds():0.##}s",
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8)
                },
                Width = width,
                Height = TrackHeight - 12,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = string.Equals(clip.Id, _selectedClipId, StringComparison.Ordinal)
                    ? Brushes.DodgerBlue
                    : Brushes.DarkSlateGray
            };
            clipButton.Click += (_, _) => SelectClip(track, clip, mediaName);
            Canvas.SetLeft(clipButton, left);
            Canvas.SetTop(clipButton, top + 6);
            _timelineCanvas.Children.Add(clipButton);
        }
    }

    private void SelectTrack(TimelineTrack track)
    {
        _selectedTrackId = track.Id;
        _selectedClipId = null;
        _statusReporter($"Selected track: {track.Name}");
        RenderTimeline();
    }

    private void SelectClip(TimelineTrack track, TimelineClip clip, string mediaName)
    {
        _selectedTrackId = track.Id;
        _selectedClipId = clip.Id;
        _statusReporter($"Selected clip: {mediaName} on {track.Name}");
        UpdatePlayhead(clip.TimelineStart.ToSeconds());
        RenderTimeline();
    }

    private void TimelineCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Point position = e.GetCurrentPoint(_timelineCanvas).Position;
        if (position.Y <= RulerHeight)
        {
            SetPlayheadFromCanvasPosition(position.X);
            return;
        }

        SetPlayheadFromCanvasPosition(position.X);
    }

    private void SetPlayheadFromCanvasPosition(double x)
    {
        double seconds = Math.Max(0, (x - TrackLabelWidth) / _zoomSlider.Value);
        UpdatePlayhead(seconds);
    }

    private void UpdatePlayhead(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
        {
            seconds = 0;
        }

        if (_snapCheckBox.IsChecked == true)
        {
            seconds = Math.Round(seconds, MidpointRounding.AwayFromZero);
        }

        _playheadSeconds = seconds;
        Canvas.SetLeft(_playhead, TrackLabelWidth + seconds * _zoomSlider.Value);
        Canvas.SetTop(_playhead, RulerHeight);
        _playhead.Height = Math.Max(0, _timelineCanvas.Height - RulerHeight);
        _playheadText.Text = $"Playhead: {seconds:0.00}s";
    }

    private void UpdateToolbar()
    {
        _zoomText.Text = $"Zoom: {_zoomSlider.Value:0} px/s";
    }

    private static string BuildRenderSignature(ProjectDocument project)
    {
        var parts = new List<string>
        {
            project.Timeline.Tracks.Count.ToString(CultureInfo.InvariantCulture)
        };
        foreach (TimelineTrack track in project.Timeline.Tracks)
        {
            parts.Add(track.Id);
            parts.Add(track.Name);
            parts.Add(track.Clips.Count.ToString(CultureInfo.InvariantCulture));
            foreach (TimelineClip clip in track.Clips)
            {
                parts.Add(clip.Id);
                parts.Add(clip.MediaId);
                parts.Add(clip.TimelineStart.Ticks.ToString(CultureInfo.InvariantCulture));
                parts.Add(clip.SourceIn.Ticks.ToString(CultureInfo.InvariantCulture));
                parts.Add(clip.SourceOut.Ticks.ToString(CultureInfo.InvariantCulture));
            }
        }

        return string.Join('|', parts);
    }

    private static string FormatSeconds(int second) =>
        TimeSpan.FromSeconds(second).ToString(second >= 3600 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);
}
