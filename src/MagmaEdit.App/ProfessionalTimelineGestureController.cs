using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.App;

/// <summary>Adds same-track move, edge trim, and click-position split gestures to the professional timeline.</summary>
internal sealed class ProfessionalTimelineGestureController : IDisposable
{
    private const double TrackLabelWidth = 128;
    private const double TrackHeight = 54;
    private const double RulerHeight = 34;
    private const double GestureThresholdPixels = 4;
    private const double TrimHandlePixels = 9;

    private readonly ProfessionalTimelineView _view;
    private readonly Func<ProjectDocument> _projectProvider;
    private readonly Action _saveProject;
    private readonly Action<string> _statusReporter;
    private GestureState? _gesture;
    private bool _disposed;

    private enum GestureKind
    {
        Move,
        TrimLeft,
        TrimRight
    }

    private sealed record GestureState(
        GestureKind Kind,
        string TrackId,
        string ClipId,
        double StartX,
        EditTime OriginalTimelineStart,
        EditTime OriginalSourceIn,
        EditTime OriginalSourceOut)
    {
        public bool Started { get; set; }
    }

    private sealed record TimelineHit(
        TimelineTrack Track,
        TimelineClip Clip,
        double ClipLeft,
        double ClipWidth,
        Point CanvasPosition);

    private ProfessionalTimelineGestureController(
        ProfessionalTimelineView view,
        Func<ProjectDocument> projectProvider,
        Action saveProject,
        Action<string> statusReporter)
    {
        _view = view;
        _projectProvider = projectProvider;
        _saveProject = saveProject;
        _statusReporter = statusReporter;
        _view.AddHandler(InputElement.PointerPressedEvent, View_PointerPressed, RoutingStrategies.Bubble);
        _view.AddHandler(InputElement.PointerMovedEvent, View_PointerMoved, RoutingStrategies.Bubble);
        _view.AddHandler(InputElement.PointerReleasedEvent, View_PointerReleased, RoutingStrategies.Bubble);
        _view.Unloaded += View_Unloaded;
    }

    public static ProfessionalTimelineGestureController Attach(
        ProfessionalTimelineView view,
        Func<ProjectDocument> projectProvider,
        Action saveProject,
        Action<string> statusReporter)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(projectProvider);
        ArgumentNullException.ThrowIfNull(saveProject);
        ArgumentNullException.ThrowIfNull(statusReporter);
        return new ProfessionalTimelineGestureController(view, projectProvider, saveProject, statusReporter);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _view.RemoveHandler(InputElement.PointerPressedEvent, View_PointerPressed);
        _view.RemoveHandler(InputElement.PointerMovedEvent, View_PointerMoved);
        _view.RemoveHandler(InputElement.PointerReleasedEvent, View_PointerReleased);
        _view.Unloaded -= View_Unloaded;
        _gesture = null;
    }

    private void View_Unloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Dispose();

    private void View_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        TimelineHit? hit = HitTestTimelineClip(e);
        if (hit is null)
        {
            _gesture = null;
            return;
        }

        Point point = hit.CanvasPosition;
        if (e.ClickCount >= 2)
        {
            SplitAtPosition(hit, point.X);
            _gesture = null;
            e.Handled = true;
            return;
        }

        GestureKind kind = DetermineGestureKind(e, hit);
        _gesture = new GestureState(
            kind,
            hit.Track.Id,
            hit.Clip.Id,
            point.X,
            hit.Clip.TimelineStart,
            hit.Clip.SourceIn,
            hit.Clip.SourceOut);
    }

    private void View_PointerMoved(object? sender, PointerEventArgs e)
    {
        GestureState? gesture = _gesture;
        if (_disposed || gesture is null)
        {
            return;
        }

        Canvas? canvas = FindTimelineCanvas();
        if (canvas is null)
        {
            return;
        }

        Point point = e.GetPosition(canvas);
        double deltaPixels = point.X - gesture.StartX;
        if (!gesture.Started && Math.Abs(deltaPixels) < GestureThresholdPixels)
        {
            return;
        }

        gesture.Started = true;
        e.Handled = true;

        ProjectDocument project = _projectProvider();
        TimelineTrack? track = project.Timeline.Tracks.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, gesture.TrackId, StringComparison.Ordinal));
        TimelineClip? clip = track?.Clips.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, gesture.ClipId, StringComparison.Ordinal));
        if (track is null || clip is null)
        {
            _gesture = null;
            return;
        }

        double pixelsPerSecond = GetPixelsPerSecond();
        double deltaSeconds = deltaPixels / pixelsPerSecond;
        switch (gesture.Kind)
        {
            case GestureKind.Move:
            {
                double target = Math.Max(0, gesture.OriginalTimelineStart.ToSeconds() + deltaSeconds);
                target = SnapSeconds(target);
                _statusReporter($"Move {track.Name}: {clip.MediaId} to {target:0.00}s. Release to apply.");
                break;
            }
            case GestureKind.TrimLeft:
            {
                double targetTimelineStart = Math.Max(0, gesture.OriginalTimelineStart.ToSeconds() + deltaSeconds);
                targetTimelineStart = SnapSeconds(targetTimelineStart);
                double effectiveDelta = targetTimelineStart - gesture.OriginalTimelineStart.ToSeconds();
                double sourceIn = Math.Max(0, gesture.OriginalSourceIn.ToSeconds() + effectiveDelta);
                _statusReporter($"Trim left: {sourceIn:0.00}s. Release to apply.");
                break;
            }
            case GestureKind.TrimRight:
            {
                double targetSourceOut = Math.Max(0, gesture.OriginalSourceOut.ToSeconds() + deltaSeconds);
                targetSourceOut = SnapSeconds(targetSourceOut);
                _statusReporter($"Trim right: {targetSourceOut:0.00}s. Release to apply.");
                break;
            }
        }
    }

    private void View_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        GestureState? gesture = _gesture;
        if (_disposed || gesture is null)
        {
            return;
        }

        _gesture = null;
        if (!gesture.Started)
        {
            return;
        }

        Canvas? canvas = FindTimelineCanvas();
        if (canvas is null)
        {
            return;
        }

        Point point = e.GetPosition(canvas);
        double pixelsPerSecond = GetPixelsPerSecond();
        double deltaSeconds = (point.X - gesture.StartX) / pixelsPerSecond;
        ProjectDocument project = _projectProvider();
        TimelineTrack? track = project.Timeline.Tracks.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, gesture.TrackId, StringComparison.Ordinal));
        TimelineClip? clip = track?.Clips.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, gesture.ClipId, StringComparison.Ordinal));
        if (track is null || clip is null)
        {
            return;
        }

        try
        {
            EditorCommandGateway gateway = new(project);
            switch (gesture.Kind)
            {
                case GestureKind.Move:
                {
                    double targetSeconds = Math.Max(0, gesture.OriginalTimelineStart.ToSeconds() + deltaSeconds);
                    targetSeconds = SnapSeconds(targetSeconds);
                    gateway.MoveClip(track.Id, clip.Id, EditTime.FromSeconds(targetSeconds));
                    _statusReporter($"Moved clip on {track.Name} to {targetSeconds:0.00}s.");
                    break;
                }
                case GestureKind.TrimLeft:
                {
                    double targetTimelineStart = Math.Max(0, gesture.OriginalTimelineStart.ToSeconds() + deltaSeconds);
                    targetTimelineStart = SnapSeconds(targetTimelineStart);
                    double effectiveDelta = targetTimelineStart - gesture.OriginalTimelineStart.ToSeconds();
                    double sourceIn = Math.Max(0, gesture.OriginalSourceIn.ToSeconds() + effectiveDelta);
                    gateway.TrimClip(
                        track.Id,
                        clip.Id,
                        EditTime.FromSeconds(sourceIn),
                        gesture.OriginalSourceOut);
                    _statusReporter($"Trimmed left edge of clip on {track.Name}.");
                    break;
                }
                case GestureKind.TrimRight:
                {
                    double targetSourceOut = Math.Max(0, gesture.OriginalSourceOut.ToSeconds() + deltaSeconds);
                    targetSourceOut = SnapSeconds(targetSourceOut);
                    gateway.TrimClip(
                        track.Id,
                        clip.Id,
                        gesture.OriginalSourceIn,
                        EditTime.FromSeconds(targetSourceOut));
                    _statusReporter($"Trimmed right edge of clip on {track.Name}.");
                    break;
                }
            }

            _saveProject();
            RefreshView();
            e.Handled = true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _statusReporter($"Timeline edit rejected: {exception.Message}");
            RefreshView();
        }
        catch (InvalidOperationException exception)
        {
            _statusReporter($"Timeline edit rejected: {exception.Message}");
            RefreshView();
        }
        catch (KeyNotFoundException exception)
        {
            _statusReporter($"Timeline edit rejected: {exception.Message}");
            RefreshView();
        }
    }

    private void SplitAtPosition(TimelineHit hit, double canvasX)
    {
        double pixelsPerSecond = GetPixelsPerSecond();
        double seconds = (canvasX - TrackLabelWidth) / pixelsPerSecond;
        seconds = Math.Max(hit.Clip.TimelineStart.ToSeconds(), seconds);
        seconds = Math.Min(hit.Clip.TimelineEnd.ToSeconds(), seconds);
        seconds = SnapSeconds(seconds);
        if (seconds <= hit.Clip.TimelineStart.ToSeconds() || seconds >= hit.Clip.TimelineEnd.ToSeconds())
        {
            _statusReporter("Split position must be inside the selected clip.");
            return;
        }

        try
        {
            EditorCommandGateway gateway = new(_projectProvider());
            gateway.SplitClip(hit.Track.Id, hit.Clip.Id, EditTime.FromSeconds(seconds));
            _saveProject();
            _statusReporter($"Split clip at {seconds:0.00}s.");
            RefreshView();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _statusReporter($"Could not split clip: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            _statusReporter($"Could not split clip: {exception.Message}");
        }
        catch (KeyNotFoundException exception)
        {
            _statusReporter($"Could not split clip: {exception.Message}");
        }
    }

    private static GestureKind DetermineGestureKind(PointerPressedEventArgs e, TimelineHit hit)
    {
        Button? button = FindSourceButton(e.Source);
        if (button is null)
        {
            return GestureKind.Move;
        }

        double relativeX = hit.CanvasPosition.X - hit.ClipLeft;
        if (relativeX <= TrimHandlePixels)
        {
            return GestureKind.TrimLeft;
        }

        if (relativeX >= hit.ClipWidth - TrimHandlePixels)
        {
            return GestureKind.TrimRight;
        }

        return GestureKind.Move;
    }

    private TimelineHit? HitTestTimelineClip(PointerEventArgs e)
    {
        Canvas? canvas = FindTimelineCanvas();
        if (canvas is null)
        {
            return null;
        }

        Button? button = FindSourceButton(e.Source);
        if (button is null || !ReferenceEquals(button.GetVisualParent(), canvas))
        {
            return null;
        }

        double buttonLeft = Canvas.GetLeft(button);
        if (!double.IsFinite(buttonLeft) || buttonLeft < TrackLabelWidth)
        {
            return null;
        }

        Point point = e.GetPosition(canvas);
        int trackIndex = (int)Math.Floor((point.Y - RulerHeight) / TrackHeight);
        ProjectDocument project = _projectProvider();
        if (trackIndex < 0 || trackIndex >= project.Timeline.Tracks.Count)
        {
            return null;
        }

        TimelineTrack track = project.Timeline.Tracks[trackIndex];
        TimelineClip? clip = track.Clips.FirstOrDefault(candidate =>
        {
            double left = TrackLabelWidth + candidate.TimelineStart.ToSeconds() * GetPixelsPerSecond();
            double width = Math.Max(36, candidate.Duration.ToSeconds() * GetPixelsPerSecond());
            return Math.Abs(left - buttonLeft) < 2 && point.X >= left && point.X <= left + width;
        });
        if (clip is null)
        {
            return null;
        }

        double clipLeft = TrackLabelWidth + clip.TimelineStart.ToSeconds() * GetPixelsPerSecond();
        double clipWidth = Math.Max(36, clip.Duration.ToSeconds() * GetPixelsPerSecond());
        return new TimelineHit(track, clip, clipLeft, clipWidth, point);
    }

    private Canvas? FindTimelineCanvas() =>
        _view.GetVisualDescendants().OfType<Canvas>().FirstOrDefault();

    private static Button? FindSourceButton(object? source)
    {
        Avalonia.Visual? current = source as Avalonia.Visual;
        while (current is not null)
        {
            if (current is Button button)
            {
                return button;
            }

            current = current.GetVisualParent();
        }

        return null;
    }

    private double GetPixelsPerSecond()
    {
        Slider? slider = _view.GetVisualDescendants().OfType<Slider>().FirstOrDefault();
        return slider is { Value: > 0 } ? slider.Value : 80;
    }

    private double SnapSeconds(double seconds)
    {
        if (!IsSnapEnabled())
        {
            return seconds;
        }

        return Math.Round(seconds, MidpointRounding.AwayFromZero);
    }

    private bool IsSnapEnabled() =>
        _view.GetVisualDescendants()
            .OfType<CheckBox>()
            .FirstOrDefault(checkBox => string.Equals(checkBox.Content?.ToString(), "Snap 1s", StringComparison.Ordinal))
            ?.IsChecked == true;

    private void RefreshView()
    {
        _view.InvalidateVisual();
        _view.GetVisualDescendants().OfType<Canvas>().FirstOrDefault();
    }
}
