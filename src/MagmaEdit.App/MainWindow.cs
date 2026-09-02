using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.App;

public sealed class MainWindow : Window
{
    private const string DefaultProjectName = "Untitled Project";
    private const string DefaultTrackName = "Video 1";

    private readonly WorkspaceLayout _workspace;
    private readonly ProjectStore _projectStore;
    private readonly ProjectDocument _project;
    private readonly string _projectPath;
    private readonly StackPanel _mediaList;
    private readonly StackPanel _timelineList;
    private readonly TextBlock _statusText;
    private readonly TextBlock _previewText;
    private readonly TextBlock _inspectorText;
    private readonly TextBlock _timelineInfoText;
    private readonly EditHistory _history = new();
    private Button? _undoButton;
    private Button? _redoButton;
    private Button? _addToTimelineButton;
    private Button? _removeClipButton;
    private Button? _splitClipButton;
    private Button? _trimClipButton;
    private TextBox? _trimSourceInBox;
    private TextBox? _trimSourceOutBox;
    private Bitmap? _previewBitmap;
    private int _previewGeneration;
    private MediaGalleryController? _mediaGallery;

    private MediaAsset? _selectedMedia;
    private TimelineTrack? _selectedTrack;
    private TimelineClip? _selectedClip;

    public MainWindow()
    {
        Title = "MagmaEdit";
        Width = 1280;
        Height = 800;
        MinWidth = 960;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _workspace = WorkspaceLayout.ForCurrentUser();
        new WorkspaceManager(_workspace).EnsureCreated();
        _projectStore = new ProjectStore(_workspace);
        _projectPath = _projectStore.GetDefaultPath(DefaultProjectName);
        _project = LoadOrCreateProject();

        _mediaList = new StackPanel { Spacing = 6 };
        _timelineList = new StackPanel { Spacing = 8 };
        _statusText = new TextBlock
        {
            Text = $"Project: {_project.Name}",
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        };
        _previewText = new TextBlock
        {
            Text = "No video selected",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        };
        _inspectorText = new TextBlock
        {
            Text = "Select a media item to inspect it.",
            TextWrapping = TextWrapping.Wrap
        };
        _timelineInfoText = new TextBlock
        {
            Text = "0 tracks",
            Opacity = 0.7
        };

        EnsureTimelineTrack();
        Closed += MainWindow_Closed;
        Content = BuildLayout();
        LoadMediaItems();
        RefreshTimeline();
        UpdateHistoryButtons();
        UpdateClipActionButtons();
    }

    internal void SetMediaGalleryController(MediaGalleryController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _mediaGallery = controller;
        _mediaGallery.Refresh();
    }

    internal ProjectDocument GetProjectForExport() => _project;

    internal void SaveProjectForExport() => SaveProject();

    internal IReadOnlyList<MediaAsset> GetMediaAssetsForGallery() => _project.Media;

    internal void SetStatusForGallery(string message) => _statusText.Text = message;

    internal bool RemoveMediaFromGallery(MediaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        bool inUse = _project.Timeline.Tracks.Any(track =>
            track.Clips.Any(clip => string.Equals(clip.MediaId, asset.Id, StringComparison.Ordinal)));
        if (inUse)
        {
            _statusText.Text = $"Cannot remove {asset.FileName}: it is used by the timeline.";
            return false;
        }

        return _project.Media.Remove(asset);
    }

    private ProjectDocument LoadOrCreateProject()
    {
        if (File.Exists(_projectPath))
        {
            try
            {
                return ProjectStore.Load(_projectPath);
            }
            catch (InvalidDataException)
            {
                return CreateRecoveryProject();
            }
            catch (JsonException)
            {
                return CreateRecoveryProject();
            }
        }

        ProjectDocument project = ProjectDocument.Create(DefaultProjectName);
        _projectStore.Save(project, _projectPath);
        return project;
    }

    private ProjectDocument CreateRecoveryProject()
    {
        string recoveryPath = Path.Combine(_workspace.Projects, $"{DefaultProjectName} - Recovery.magmaedit.json");
        ProjectDocument recovery = ProjectDocument.Create($"{DefaultProjectName} - Recovery");
        _projectStore.Save(recovery, recoveryPath);
        return recovery;
    }

    private void EnsureTimelineTrack()
    {
        if (_project.Timeline.Tracks.Count == 0)
        {
            _selectedTrack = _project.Timeline.AddTrack(DefaultTrackName);
            SaveProject();
        }
        else
        {
            _selectedTrack = _project.Timeline.Tracks[0];
        }
    }

    private Grid BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,220"),
            ColumnDefinitions = new ColumnDefinitions("260,*,280"),
            Margin = new Thickness(12)
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "MagmaEdit",
                    FontSize = 24,
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

        _undoButton = new Button { Content = "Undo" };
        _undoButton.Click += (_, _) => Undo();
        header.Children.Add(_undoButton);

        _redoButton = new Button { Content = "Redo" };
        _redoButton.Click += (_, _) => Redo();
        header.Children.Add(_redoButton);

        var addTrackButton = new Button { Content = "Add Track" };
        addTrackButton.Click += (_, _) => AddTrack();
        header.Children.Add(addTrackButton);

        Grid.SetColumnSpan(header, 3);
        root.Children.Add(header);

        var importButton = new Button
        {
            Content = "Import Video",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        importButton.Click += ImportButton_Click;

        var media = new Border
        {
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Media", FontSize = 18, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = "Imported videos are stored in Content Creation\\Media.", TextWrapping = TextWrapping.Wrap },
                    importButton,
                    _statusText,
                    new Separator(),
                    _mediaList
                }
            }
        };
        Grid.SetRow(media, 1);
        root.Children.Add(media);

        var previewCanvas = new Border
        {
            Width = 360,
            Height = 640,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Black,
            Child = _previewText
        };

        var preview = new Border
        {
            Margin = new Thickness(12, 0),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            Child = previewCanvas
        };
        Grid.SetRow(preview, 1);
        Grid.SetColumn(preview, 1);
        root.Children.Add(preview);

        _addToTimelineButton = new Button
        {
            Content = "Add Selected Video to Timeline",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false
        };
        _addToTimelineButton.Click += (_, _) => AddSelectedMediaToTimeline();

        _removeClipButton = new Button
        {
            Content = "Remove Selected Clip",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false
        };
        _removeClipButton.Click += (_, _) => RemoveSelectedClip();

        _splitClipButton = new Button
        {
            Content = "Split Selected Clip at Midpoint",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false
        };
        _splitClipButton.Click += (_, _) => SplitSelectedClip();

        _trimSourceInBox = new TextBox
        {
            PlaceholderText = "Source In (seconds)",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _trimSourceOutBox = new TextBox
        {
            PlaceholderText = "Source Out (seconds)",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _trimClipButton = new Button
        {
            Content = "Trim Selected Clip",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false
        };
        _trimClipButton.Click += (_, _) => TrimSelectedClip();

        var trimPanel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Trim source range", FontWeight = FontWeight.SemiBold },
                _trimSourceInBox,
                _trimSourceOutBox,
                _trimClipButton
            }
        };

        var inspector = new Border
        {
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = "Inspector", FontSize = 18, FontWeight = FontWeight.SemiBold },
                        _inspectorText,
                        _addToTimelineButton,
                        _removeClipButton,
                        _splitClipButton,
                        trimPanel
                    }
                }
            }
        };
        Grid.SetRow(inspector, 1);
        Grid.SetColumn(inspector, 2);
        root.Children.Add(inspector);

        var timeline = new Border
        {
            Margin = new Thickness(0, 12, 0, 0),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock { Text = "Timeline", FontSize = 18, FontWeight = FontWeight.SemiBold },
                            _timelineInfoText
                        }
                    },
                    _timelineList
                }
            }
        };
        Grid.SetRow(timeline, 2);
        Grid.SetColumnSpan(timeline, 3);
        root.Children.Add(timeline);

        return root;
    }

    private async void ImportButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!StorageProvider.CanOpen)
            {
                _statusText.Text = "File selection is not available on this system.";
                return;
            }

            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Video",
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType("Video Files")
                    {
                        Patterns = ["*.mp4", "*.mov", "*.m4v", "*.webm", "*.mkv", "*.avi"]
                    }
                ]
            });

            if (files.Count == 0)
            {
                return;
            }

            int imported = 0;
            int alreadyImported = 0;
            int failed = 0;
            MediaAsset? lastImportedAsset = null;
            var importer = new MediaImportService(_workspace);

            foreach (IStorageFile file in files)
            {
                string? localPath = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(localPath))
                {
                    failed++;
                    continue;
                }

                string normalizedSource = Path.GetFullPath(localPath);
                if (_project.Media.Any(asset =>
                    string.Equals(asset.SourcePath, normalizedSource, StringComparison.OrdinalIgnoreCase)))
                {
                    alreadyImported++;
                    continue;
                }

                try
                {
                    MediaAsset asset = importer.Import(normalizedSource);
                    _project.Media.Add(asset);
                    lastImportedAsset = asset;
                    imported++;
                }
                catch (Exception exception) when (
                    exception is FileNotFoundException or
                    NotSupportedException or
                    InvalidDataException or
                    IOException or
                    UnauthorizedAccessException)
                {
                    failed++;
                }
            }

            if (imported > 0 && lastImportedAsset is not null)
            {
                SaveProject();
                SelectMedia(lastImportedAsset);
                _mediaGallery?.Refresh();
            }

            _statusText.Text = BuildImportStatus(imported, alreadyImported, failed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _statusText.Text = exception.Message;
        }
    }

    private void AddTrack()
    {
        var command = new AddTimelineTrackCommand(_project.Timeline, $"Video {_project.Timeline.Tracks.Count + 1}");
        _history.Execute(command);
        _selectedTrack = command.Track;
        _selectedClip = null;
        SaveProject();
        _statusText.Text = $"Added track: {_selectedTrack.Name}";
        UpdateHistoryButtons();
        UpdateClipActionButtons();
    }

    private void AddSelectedMediaToTimeline()
    {
        MediaAsset? asset = _selectedMedia;
        TimelineTrack? track = _selectedTrack;
        if (asset is null || track is null)
        {
            _statusText.Text = "Select a video and a timeline track first.";
            return;
        }

        if (asset.Metadata is not { } metadata || metadata.Duration <= TimeSpan.Zero || !double.IsFinite(metadata.Duration.TotalSeconds))
        {
            _statusText.Text = $"Cannot add {asset.FileName}: its duration is unavailable or invalid.";
            return;
        }

        EditTime duration;
        EditTime timelineStart;
        try
        {
            duration = EditTime.FromSeconds(metadata.Duration.TotalSeconds);
            timelineStart = track.Clips.Count == 0
                ? EditTime.Zero
                : track.Clips.Max(clip => clip.TimelineEnd);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _statusText.Text = $"Cannot add {asset.FileName}: {exception.Message}";
            return;
        }
        catch (OverflowException exception)
        {
            _statusText.Text = $"Cannot add {asset.FileName}: the timeline range is too large. {exception.Message}";
            return;
        }

        var command = new InsertTimelineClipCommand(
            new TimelineEditor(_project.Timeline),
            track.Id,
            asset.Id,
            timelineStart,
            EditTime.Zero,
            duration);

        try
        {
            _history.Execute(command);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException or OverflowException)
        {
            _statusText.Text = $"Could not add {asset.FileName}: {exception.Message}";
            return;
        }

        _selectedClip = track.Clips.FirstOrDefault(clip =>
            string.Equals(clip.Id, command.Clip.Id, StringComparison.Ordinal));
        SaveProject();
        _statusText.Text = $"Added {asset.FileName} to {track.Name}.";
        UpdateHistoryButtons();
        UpdateClipActionButtons();
    }

    private void RemoveSelectedClip()
    {
        TimelineTrack? track = _selectedTrack;
        TimelineClip? clip = _selectedClip;
        if (track is null || clip is null)
        {
            _statusText.Text = "Select a timeline clip first.";
            return;
        }

        if (!track.Clips.Any(existing => string.Equals(existing.Id, clip.Id, StringComparison.Ordinal)))
        {
            _statusText.Text = "The selected clip is no longer on the selected track.";
            _selectedClip = null;
            UpdateClipActionButtons();
            return;
        }

        try
        {
            _history.Execute(new RemoveTimelineClipCommand(
                new TimelineEditor(_project.Timeline),
                track.Id,
                clip.Id));
        }
        catch (KeyNotFoundException exception)
        {
            _statusText.Text = exception.Message;
            return;
        }

        _selectedClip = null;
        SaveProject();
        _statusText.Text = $"Removed clip from {track.Name}.";
        UpdateHistoryButtons();
        UpdateClipActionButtons();
    }

    private void SplitSelectedClip()
    {
        TimelineTrack? track = _selectedTrack;
        TimelineClip? clip = _selectedClip;
        if (track is null || clip is null)
        {
            _statusText.Text = "Select a timeline clip first.";
            return;
        }

        EditTime midpoint = clip.TimelineStart + new EditTime(clip.Duration.Ticks / 2);
        if (midpoint <= clip.TimelineStart || midpoint >= clip.TimelineEnd)
        {
            _statusText.Text = "The selected clip is too short to split.";
            return;
        }

        var command = new SplitTimelineClipCommand(
            new TimelineEditor(_project.Timeline),
            track.Id,
            clip.Id,
            midpoint);

        try
        {
            _history.Execute(command);
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
        {
            _statusText.Text = $"Could not split clip: {exception.Message}";
            return;
        }

        _selectedClip = track.Clips.FirstOrDefault(candidate =>
            candidate.TimelineStart == midpoint);
        SaveProject();
        _statusText.Text = $"Split clip on {track.Name}.";
        UpdateHistoryButtons();
        UpdateClipActionButtons();
    }

    private void TrimSelectedClip()
    {
        TimelineTrack? track = _selectedTrack;
        TimelineClip? clip = _selectedClip;
        if (track is null || clip is null || _trimSourceInBox is null || _trimSourceOutBox is null)
        {
            _statusText.Text = "Select a timeline clip first.";
            return;
        }

        if (!double.TryParse(_trimSourceInBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double sourceInSeconds) ||
            !double.TryParse(_trimSourceOutBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double sourceOutSeconds))
        {
            _statusText.Text = "Enter valid source-in and source-out seconds.";
            return;
        }

        if (!double.IsFinite(sourceInSeconds) || !double.IsFinite(sourceOutSeconds) ||
            sourceInSeconds < 0 || sourceOutSeconds < 0 || sourceOutSeconds <= sourceInSeconds)
        {
            _statusText.Text = "Source Out must be greater than Source In, and both must be finite and non-negative.";
            return;
        }

        EditTime sourceIn;
        EditTime sourceOut;
        try
        {
            sourceIn = EditTime.FromSeconds(sourceInSeconds);
            sourceOut = EditTime.FromSeconds(sourceOutSeconds);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _statusText.Text = $"Enter a trim range within the supported time limit: {exception.Message}";
            return;
        }
        catch (OverflowException exception)
        {
            _statusText.Text = $"Enter a trim range within the supported time limit: {exception.Message}";
            return;
        }

        var command = new TrimTimelineClipCommand(
            new TimelineEditor(_project.Timeline),
            track.Id,
            clip.Id,
            sourceIn,
            sourceOut);

        try
        {
            _history.Execute(command);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _statusText.Text = $"Could not trim clip: {exception.Message}";
            return;
        }
        catch (InvalidOperationException exception)
        {
            _statusText.Text = $"Could not trim clip: {exception.Message}";
            return;
        }

        _selectedClip = track.Clips.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, clip.Id, StringComparison.Ordinal));
        SaveProject();
        _statusText.Text = $"Trimmed clip on {track.Name}.";
        UpdateHistoryButtons();
        UpdateClipActionButtons();
    }

    private void Undo()
    {
        if (!_history.Undo())
        {
            _statusText.Text = "Nothing to undo.";
            UpdateHistoryButtons();
            return;
        }

        SaveProject();
        _selectedClip = null;
        _statusText.Text = "Undo complete.";
        UpdateHistoryButtons();
        UpdateClipActionButtons();
    }

    private void Redo()
    {
        if (!_history.Redo())
        {
            _statusText.Text = "Nothing to redo.";
            UpdateHistoryButtons();
            return;
        }

        SaveProject();
        _selectedClip = null;
        _statusText.Text = "Redo complete.";
        UpdateHistoryButtons();
        UpdateClipActionButtons();
    }

    private void UpdateHistoryButtons()
    {
        if (_undoButton is not null)
        {
            _undoButton.IsEnabled = _history.CanUndo;
        }

        if (_redoButton is not null)
        {
            _redoButton.IsEnabled = _history.CanRedo;
        }
    }

    private void UpdateClipActionButtons()
    {
        bool clipSelected = _selectedClip is not null && _selectedTrack is not null;
        if (_removeClipButton is not null)
        {
            _removeClipButton.IsEnabled = clipSelected;
        }

        if (_splitClipButton is not null)
        {
            _splitClipButton.IsEnabled = clipSelected && _selectedClip!.Duration.Ticks >= 2;
        }

        if (_trimClipButton is not null)
        {
            _trimClipButton.IsEnabled = clipSelected;
        }

        if (_trimSourceInBox is not null && _trimSourceOutBox is not null && clipSelected)
        {
            _trimSourceInBox.Text = _selectedClip!.SourceIn.ToSeconds().ToString("0.###", CultureInfo.InvariantCulture);
            _trimSourceOutBox.Text = _selectedClip.SourceOut.ToSeconds().ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

    private void LoadMediaItems()
    {
        foreach (MediaAsset asset in _project.Media)
        {
            AddMediaItem(asset);
        }
    }

    private void AddMediaItem(MediaAsset asset)
    {
        if (_mediaGallery is not null)
        {
            _mediaGallery.Refresh();
            return;
        }

        var item = new Button
        {
            Content = asset.FileName,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ToolTip.SetTip(item, asset.LibraryPath);
        item.Click += (_, e) =>
        {
            SelectMedia(asset);
            e.Handled = true;
        };
        _mediaList.Children.Add(item);
    }

    internal void SelectMedia(MediaAsset asset)
    {
        _selectedMedia = asset;
        _selectedClip = null;
        int generation = ++_previewGeneration;
        ShowPreviewLoadingState(asset);
        if (_addToTimelineButton is not null)
        {
            _addToTimelineButton.IsEnabled = asset.Metadata is { } selectedMetadata && selectedMetadata.Duration > TimeSpan.Zero;
        }
        UpdateClipActionButtons();

        if (asset.Metadata is { } metadata)
        {
            _inspectorText.Text =
                $"Name: {asset.FileName}\n\n" +
                $"Size: {metadata.Width}×{metadata.Height}\n\n" +
                $"Duration: {metadata.Duration.TotalSeconds:0.###} s\n\n" +
                $"FPS: {metadata.FramesPerSecond:0.###}\n\n" +
                $"Video codec: {metadata.VideoCodec}\n\n" +
                $"Audio: {(metadata.HasAudio ? metadata.AudioCodec : "None")}\n\n" +
                $"Library: {asset.LibraryPath}";
        }
        else
        {
            _inspectorText.Text =
                $"Name: {asset.FileName}\n\n" +
                $"Source: {asset.SourcePath}\n\n" +
                $"Library: {asset.LibraryPath}";
        }

        _statusText.Text = $"Selected: {asset.FileName}";
        _ = LoadPreviewAsync(asset, generation);
    }

    private void ShowPreviewLoadingState(MediaAsset asset)
    {
        _previewText.IsVisible = true;
        _previewText.Text = $"Loading preview…\n{asset.FileName}";
        if (_previewText.Parent is Border previewCanvas)
        {
            previewCanvas.Background = Brushes.Black;
        }
    }

    private async Task LoadPreviewAsync(MediaAsset asset, int generation)
    {
        try
        {
            DecodedPreviewFrame frame = await MediaPreviewService.DecodeFirstFrameAsync(asset.LibraryPath);
            if (generation != _previewGeneration || !ReferenceEquals(asset, _selectedMedia))
            {
                return;
            }

            WriteableBitmap bitmap = CreatePreviewBitmap(frame);
            Bitmap? previous = _previewBitmap;
            _previewBitmap = bitmap;

            if (_previewText.Parent is Border previewCanvas)
            {
                previewCanvas.Background = new ImageBrush(bitmap)
                {
                    Stretch = Stretch.Uniform
                };
            }

            _previewText.IsVisible = false;
            previous?.Dispose();
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or
            InvalidDataException or
            IOException or
            UnauthorizedAccessException)
        {
            if (generation != _previewGeneration || !ReferenceEquals(asset, _selectedMedia))
            {
                return;
            }

            _previewText.IsVisible = true;
            _previewText.Text = $"Preview unavailable\n{exception.Message}";
            _statusText.Text = $"Preview unavailable: {asset.FileName}";
        }
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

    private void RefreshTimeline()
    {
        _timelineList.Children.Clear();
        foreach (TimelineTrack track in _project.Timeline.Tracks)
        {
            var trackPanel = new StackPanel { Spacing = 4 };
            var trackHeader = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            var selectTrack = new Button { Content = track.Name };
            selectTrack.Click += (_, _) =>
            {
                _selectedTrack = track;
                _selectedClip = null;
                _statusText.Text = $"Selected track: {track.Name}";
                RefreshTimeline();
                UpdateClipActionButtons();
            };
            trackHeader.Children.Add(selectTrack);
            trackHeader.Children.Add(new TextBlock
            {
                Text = track.Clips.Count == 0 ? "Empty" : $"{track.Clips.Count} clip(s)",
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.7
            });
            trackPanel.Children.Add(trackHeader);

            foreach (TimelineClip clip in track.Clips)
            {
                MediaAsset? media = _project.Media.FirstOrDefault(asset =>
                    string.Equals(asset.Id, clip.MediaId, StringComparison.Ordinal));
                string mediaName = media?.FileName ?? $"Missing media {clip.MediaId}";
                string label = $"{mediaName}  |  {clip.TimelineStart.ToSeconds():0.##}s - {clip.TimelineEnd.ToSeconds():0.##}s";
                var clipButton = new Button
                {
                    Content = label,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                clipButton.Click += (_, _) =>
                {
                    _selectedTrack = track;
                    _selectedClip = clip;
                    _statusText.Text = $"Selected clip: {mediaName}";
                    _inspectorText.Text =
                        $"Clip ID: {clip.Id}\n\n" +
                        $"Track: {track.Name}\n\n" +
                        $"Start: {clip.TimelineStart.ToSeconds():0.###} s\n\n" +
                        $"Duration: {clip.Duration.ToSeconds():0.###} s\n\n" +
                        $"Source In: {clip.SourceIn.ToSeconds():0.###} s\n" +
                        $"Source Out: {clip.SourceOut.ToSeconds():0.###} s";
                    UpdateClipActionButtons();
                };
                trackPanel.Children.Add(clipButton);
            }

            _timelineList.Children.Add(new Border
            {
                Padding = new Thickness(8),
                BorderThickness = new Thickness(1),
                Child = trackPanel
            });
        }

        _timelineInfoText.Text = $"{_project.Timeline.Tracks.Count} track(s) • {_project.Timeline.Tracks.Sum(track => track.Clips.Count)} clip(s) • {_project.Timeline.Width}×{_project.Timeline.Height} @ {_project.Timeline.FrameRateNumerator}/{_project.Timeline.FrameRateDenominator}";
    }

    private void SaveProject()
    {
        _project.ModifiedUtc = DateTimeOffset.UtcNow;
        _projectStore.Save(_project, _projectPath);
        RefreshTimeline();
    }

    private static string BuildImportStatus(int imported, int alreadyImported, int failed)
    {
        if (imported == 0 && alreadyImported == 0 && failed == 0)
        {
            return "No local video files were imported.";
        }

        string result = $"Imported {imported} video{(imported == 1 ? string.Empty : "s")}.";
        if (alreadyImported > 0)
        {
            result += $" Skipped {alreadyImported} already imported video{(alreadyImported == 1 ? string.Empty : "s")}.";
        }

        return failed == 0
            ? result
            : $"{result} Failed {failed} video{(failed == 1 ? string.Empty : "s")}.";
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _previewGeneration++;
        _previewBitmap?.Dispose();
        _previewBitmap = null;
        SaveProject();
    }
}