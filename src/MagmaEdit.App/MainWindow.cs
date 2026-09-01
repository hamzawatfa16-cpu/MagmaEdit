using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.App;

public sealed class MainWindow : Window
{
    private const string DefaultProjectName = "Untitled Project";

    private readonly WorkspaceLayout _workspace;
    private readonly ProjectStore _projectStore;
    private readonly ProjectDocument _project;
    private readonly string _projectPath;
    private readonly StackPanel _mediaList;
    private readonly TextBlock _statusText;
    private readonly TextBlock _previewText;
    private readonly TextBlock _inspectorText;

    private MediaAsset? _selectedMedia;

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

        Closed += MainWindow_Closed;
        Content = BuildLayout();
        LoadMediaItems();
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

    private Grid BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,180"),
            ColumnDefinitions = new ColumnDefinitions("260,*,280"),
            Margin = new Thickness(12)
        };

        var header = new Border
        {
            Padding = new Thickness(16, 12),
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = "MagmaEdit",
                FontSize = 24,
                FontWeight = FontWeight.SemiBold
            }
        };
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
                    new TextBlock { Text = "Imported videos are stored in Content Creation\\Media." },
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
            Child = new TextBlock
            {
                Text = "Timeline is not connected yet.",
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.75
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
                imported++;
            }

            if (imported > 0)
            {
                SelectMedia(_project.Media[^imported]);
                _projectStore.Save(_project, _projectPath);
            }

            _statusText.Text = BuildImportStatus(imported, alreadyImported);
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            _statusText.Text = exception.Message;
        }
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
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ToolTip = asset.LibraryPath
        };
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

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _projectStore.Save(_project, _projectPath);
    }
}
