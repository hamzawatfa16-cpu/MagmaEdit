using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.App;

/// <summary>Provides Ctrl+D duplication for the currently focused timeline clip.</summary>
internal sealed class TimelineClipDuplicateShortcutController
{
    private readonly MainWindow _window;
    private StackPanel? _timelineList;
    private bool _attached;

    private TimelineClipDuplicateShortcutController(MainWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public static TimelineClipDuplicateShortcutController Attach(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var controller = new TimelineClipDuplicateShortcutController(window);
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
            _window.SetStatusForGallery("Timeline duplicate shortcut could not attach.");
            return;
        }

        _window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        _attached = true;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (!_attached)
        {
            return;
        }

        _window.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _attached = false;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_timelineList is null || (e.KeyModifiers & KeyModifiers.Control) == 0 || e.Key != Key.D)
        {
            return;
        }

        Button? focusedButton = FindButton(e.Source as Visual);
        if (focusedButton is null || !TryGetClip(focusedButton, out (TimelineTrack Track, TimelineClip Clip) selection))
        {
            return;
        }

        try
        {
            var gateway = new EditorCommandGateway(_window.GetProjectForExport());
            TimelineClip duplicate = gateway.DuplicateClip(selection.Track.Id, selection.Clip.Id);
            _window.SaveProjectForExport();
            LiveEditorPipeUiRefresh.Refresh(
                _window,
                $"Duplicated clip to the end of {selection.Track.Name}.");
            SelectClipButton(duplicate.Id);
            e.Handled = true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            KeyNotFoundException or
            IOException or
            UnauthorizedAccessException or
            InvalidDataException)
        {
            _window.SetStatusForGallery($"Could not duplicate clip: {exception.Message}");
        }
    }

    private void SelectClipButton(string clipId)
    {
        Dictionary<Button, (TimelineTrack Track, TimelineClip Clip)> clips = BuildClipMap();
        Button? duplicateButton = clips.FirstOrDefault(pair =>
            string.Equals(pair.Value.Clip.Id, clipId, StringComparison.Ordinal)).Key;
        duplicateButton?.Focus();
    }

    private bool TryGetClip(Button button, out (TimelineTrack Track, TimelineClip Clip) clip)
    {
        Dictionary<Button, (TimelineTrack Track, TimelineClip Clip)> clips = BuildClipMap();
        if (clips.TryGetValue(button, out clip))
        {
            return true;
        }

        clip = default;
        return false;
    }

    private Dictionary<Button, (TimelineTrack Track, TimelineClip Clip)> BuildClipMap()
    {
        var result = new Dictionary<Button, (TimelineTrack Track, TimelineClip Clip)>();
        StackPanel? timelineList = _timelineList;
        if (timelineList is null)
        {
            return result;
        }

        List<TimelineTrack> tracks = _window.GetProjectForExport().Timeline.Tracks;
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

    private static Button? FindButton(Visual? source)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Button button)
            {
                return button;
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
}
