using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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

    private MediaAsset? _selectedMedia;
    private TimelineTrack? _selectedTrack;

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

        var undoButton = new Button { Content = "Undo" };
        undoButton.Click += (_, _) => Undo();
        header.Children.Add(undoButton);

        var redoButton = new Button { Content = "Redo" };
        redoButton.Click += (_, _) => Redo();
        header.Children.Add(redoButton);

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
            MaxWidth = 420,
            MaxHeight = 680,
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

        var inspector = new Border
        {
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Inspector", FontSize = 18, FontWeight = FontWeight.SemiBold },
                    _inspectorText
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
            MediaAsset? lastImportedAsset = null;
            var importer = new MediaImportService(_workspace);

            foreach (IStorageFile file in files)
            {
                string? localPath = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(localPath))
                {
                    continue;
                }

                string normalizedSource = Path.GetFullPath(localPath);
                if (_project.Media.Any(asset =>
                    string.Equals(asset.SourcePath, normalizedSource, StringComparison.OrdinalIgnoreCase)))
                {
                    alreadyImported++;
                    continue;
                }

                MediaAsset asset = importer.Import(normalizedSource);
                _project.Media.Add(asset);
                AddMediaItem(asset);
                lastImportedAsset = asset;
                imported++;
            }

            if (imported > 0 && lastImportedAsset is not null)
            {
                SelectMedia(lastImportedAsset);
                SaveProject();
            }

            _statusText.Text = BuildImportStatus(imported, alreadyImported);
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            _statusText.Text = exception.Message;
        }
    }

    private void AddTrack()
    {
        var command = new AddTimelineTrackCommand(_project.Timeline, $"Video {_project.Timeline.Tracks.Count + 1}");
        _history.Execute(command);
        _selectedTrack = command.Track;
        SaveProject();
        _statusText.Text = $"Added track: {_selectedTrack.Name}";
        UpdateHistoryButtons();
    }

    private void Undo()
    {
        if (!_history.Undo())
        {
            _statusText.Text = "Nothing to undo.";
            return;
        }

        SaveProject();
        _statusText.Text = "Undo complete.";
        UpdateHistoryButtons();
    }

    private void Redo()
    {
        if (!_history.Redo())
        {
            _statusText.Text = "Nothing to redo.";
            return;
        }

        SaveProject();
        _statusText.Text = "Redo complete.";
        UpdateHistoryButtons();
    }

    private void UpdateHistoryButtons()
    {
        // Button state is intentionally refreshed through the next layout pass.
        // The command history itself is the authoritative state.
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
        var item = new Button
        {
            Content = asset.FileName,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ToolTip.SetTip(item, asset.LibraryPath);
        item.Click += (_, _) => SelectMedia(asset);
        _mediaList.Children.Add(item);
    }

    private void SelectMedia(MediaAsset asset)
    {
        _selectedMedia = asset;
        _previewText.Text = asset.FileName;
        _inspectorText.Text =
            $"Name: {asset.FileName}\n\n" +
            $"Source: {asset.SourcePath}\n\n" +
            $"Library: {asset.LibraryPath}";
        _statusText.Text = $"Selected: {asset.FileName}";
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
                _statusText.Text = $"Selected track: {track.Name}";
                RefreshTimeline();
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
                    _statusText.Text = $"Selected clip: {mediaName}";
                    _inspectorText.Text =
                        $"Clip ID: {clip.Id}\n\n" +
                        $"Track: {track.Name}\n\n" +
                        $"Start: {clip.TimelineStart.ToSeconds():0.###} s\n" +
                        $"Duration: {clip.Duration.ToSeconds():0.###} s\n" +
                        $"Source In: {clip.SourceIn.ToSeconds():0.###} s\n" +
                        $"Source Out: {clip.SourceOut.ToSeconds():0.###} s";
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

    private static string BuildImportStatus(int imported, int alreadyImported)
    {
        if (imported == 0 && alreadyImported == 0)
        {
            return "No local video files were imported.";
        }

        string result = $"Imported {imported} video{(imported == 1 ? string.Empty : "s")}.";
        return alreadyImported == 0
            ? result
            : $"{result} Skipped {alreadyImported} already imported video{(alreadyImported == 1 ? string.Empty : "s")}.";
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SaveProject();
    }
}
