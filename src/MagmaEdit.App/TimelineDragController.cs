using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.App;

/// <summary>Provides mouse drag-and-drop for moving timeline clips between tracks.</summary>
internal sealed class TimelineDragController
{
    private const double DragThresholdPixels = 8;

    private readonly MainWindow _window;
    private StackPanel? _timelineList;
    private Button? _pressedClipButton;
    private Pointer? _activePointer;
    private Point _pressPosition;
    private bool _dragging;
    private bool _attached;

    private TimelineDragController(MainWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public static TimelineDragController Attach(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var controller = new TimelineDragController(window);
        window.Opened += controller.Window_Opened;
        window.Closed += controller.Window_Closed;
        return controller;
    }

    private void Window_Opened(object? sender, EventArgs e)
    {
        if (_attached)
        {
            return;
        }

        _timelineList = FindTimelineList(_window);
        if (_timelineList is null)
        {
            _window.SetStatusForGallery("Timeline drag-and-drop could not attach.");
            return;
        }

        _window.AddHandler(
            InputElement.PointerPressedEvent,
            OnPointerPressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        _window.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        _window.AddHandler(
            InputElement.PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        _window.PointerCaptureLost += Window_PointerCaptureLost;
        _attached = true;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        ResetDragState();
        if (!_attached)
        {
            return;
        }

        _window.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _window.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        _window.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        _window.PointerCaptureLost -= Window_PointerCaptureLost;
        _attached = false;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_timelineList is null || _activePointer is not null)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(_window);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Button? button = FindButton(e.Source as Visual);
        if (button is null || !TryGetClip(button, out _))
        {
            return;
        }

        _pressedClipButton = button;
        _activePointer = e.Pointer;
        _pressPosition = point.Position;
        _dragging = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_activePointer is null || !ReferenceEquals(e.Pointer, _activePointer) || _pressedClipButton is null)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(_window);
        Vector delta = point.Position - _pressPosition;
        if (!_dragging && delta.Length < DragThresholdPixels)
        {
            return;
        }

        if (!_dragging)
        {
            _dragging = true;
            _activePointer.Capture(_window);
            _pressedClipButton.Opacity = 0.65;
            _window.SetStatusForGallery("Dragging clip… release over another track to move it.");
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_activePointer is null || !ReferenceEquals(e.Pointer, _activePointer))
        {
            return;
        }

        bool wasDragging = _dragging;
        Pointer? pointer = _activePointer;
        Point dropPosition = e.GetPosition(_window);
        Button? pressedButton = _pressedClipButton;

        if (pointer is not null)
        {
            pointer.Capture(null);
        }

        ResetDragState(restoreButton: !wasDragging);

        if (!wasDragging || pressedButton is null || _timelineList is null)
        {
            return;
        }

        e.Handled = true;
        PerformDrop(pressedButton, dropPosition);
    }

    private void Window_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_activePointer is not null && ReferenceEquals(e.Pointer, _activePointer))
        {
            ResetDragState();
        }
    }

    private void PerformDrop(Button sourceButton, Point windowPosition)
    {
        if (_timelineList is null || !TryGetClip(sourceButton, out (TimelineTrack Track, TimelineClip Clip) source))
        {
            return;
        }

        Point timelinePosition = _window.TranslatePoint(windowPosition, _timelineList) ?? new Point(double.NaN, double.NaN);
        TimelineTrack? destinationTrack = GetTrackAtPosition(timelinePosition);
        if (destinationTrack is null)
        {
            _window.SetStatusForGallery("Drop the clip inside a timeline track.");
            return;
        }

        if (string.Equals(source.Track.Id, destinationTrack.Id, StringComparison.Ordinal))
        {
            _window.SetStatusForGallery("The clip is already on that track.");
            return;
        }

        TimelineClip sourceClip = source.Track.Clips.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, source.Clip.Id, StringComparison.Ordinal)) ?? source.Clip;

        try
        {
            var gateway = new EditorCommandGateway(_window.GetProjectForExport());
            gateway.MoveClipToTrack(
                source.Track.Id,
                destinationTrack.Id,
                sourceClip.Id,
                sourceClip.TimelineStart);
            _window.SaveProjectForExport();
            SelectMovedClip(destinationTrack, sourceClip.Id);
            _window.SetStatusForGallery($"Moved clip to {destinationTrack.Name}.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            KeyNotFoundException or
            ArgumentOutOfRangeException or
            IOException or
            UnauthorizedAccessException or
            InvalidDataException)
        {
            _window.SetStatusForGallery($"Could not move clip: {exception.Message}");
        }
    }

    private void SelectMovedClip(TimelineTrack destinationTrack, string clipId)
    {
        if (_timelineList is null)
        {
            return;
        }

        IReadOnlyDictionary<Button, (TimelineTrack Track, TimelineClip Clip)> clips = BuildClipMap();
        Button? movedButton = clips.FirstOrDefault(pair =>
            string.Equals(pair.Value.Track.Id, destinationTrack.Id, StringComparison.Ordinal) &&
            string.Equals(pair.Value.Clip.Id, clipId, StringComparison.Ordinal)).Key;

        movedButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private bool TryGetClip(Button button, out (TimelineTrack Track, TimelineClip Clip) clip)
    {
        IReadOnlyDictionary<Button, (TimelineTrack Track, TimelineClip Clip)> clips = BuildClipMap();
        if (clips.TryGetValue(button, out clip))
        {
            return true;
        }

        clip = default;
        return false;
    }

    private IReadOnlyDictionary<Button, (TimelineTrack Track, TimelineClip Clip)> BuildClipMap()
    {
        var result = new Dictionary<Button, (TimelineTrack Track, TimelineClip Clip)>();
        StackPanel? timelineList = _timelineList;
        if (timelineList is null)
        {
            return result;
        }

        IReadOnlyList<TimelineTrack> tracks = _window.GetProjectForExport().Timeline.Tracks;
        int trackCount = Math.Min(tracks.Count, timelineList.Children.Count);
        for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
        {
            TimelineTrack track = tracks[trackIndex];
            if (timelineList.Children[trackIndex] is not Border trackBorder ||
                trackBorder.Child is not StackPanel trackPanel)
            {
                continue;
            }

            int clipIndex = 0;
            foreach (Control child in trackPanel.Children.Skip(1))
            {
                if (child is Button button && clipIndex < track.Clips.Count)
                {
                    result[button] = (track, track.Clips[clipIndex]);
                    clipIndex++;
                }
            }
        }

        return result;
    }

    private TimelineTrack? GetTrackAtPosition(Point timelinePosition)
    {
        if (_timelineList is null || !double.IsFinite(timelinePosition.X) || !double.IsFinite(timelinePosition.Y))
        {
            return null;
        }

        IReadOnlyList<TimelineTrack> tracks = _window.GetProjectForExport().Timeline.Tracks;
        int count = Math.Min(tracks.Count, _timelineList.Children.Count);
        for (int index = 0; index < count; index++)
        {
            if (_timelineList.Children[index] is Border trackBorder && trackBorder.Bounds.Contains(timelinePosition))
            {
                return tracks[index];
            }
        }

        return null;
    }

    private static StackPanel? FindTimelineList(MainWindow window)
    {
        Visual? root = window.Content as Visual;
        if (root is null)
        {
            return null;
        }

        foreach (StackPanel panel in root.GetVisualDescendants().OfType<StackPanel>())
        {
            if (panel.Children.Count < 2 || panel.Children[0] is not StackPanel header)
            {
                continue;
            }

            bool isTimelineHeader = header.Children
                .OfType<TextBlock>()
                .Any(text => string.Equals(text.Text, "Timeline", StringComparison.Ordinal));
            if (isTimelineHeader && panel.Children[1] is StackPanel timelineList)
            {
                return timelineList;
            }
        }

        return null;
    }

    private void ResetDragState(bool restoreButton = true)
    {
        if (restoreButton && _pressedClipButton is not null)
        {
            _pressedClipButton.Opacity = 1;
        }

        _pressedClipButton = null;
        _activePointer = null;
        _pressPosition = default;
        _dragging = false;
    }
}
