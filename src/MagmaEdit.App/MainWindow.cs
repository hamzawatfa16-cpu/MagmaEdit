using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.App;

public sealed class MainWindow : Window
{
    private readonly WorkspaceLayout _workspace;
    private readonly StackPanel _mediaList;
    private readonly TextBlock _statusText;

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
        _mediaList = new StackPanel { Spacing = 6 };
        _statusText = new TextBlock { Text = "Ready", Opacity = 0.75, TextWrapping = TextWrapping.Wrap };

        Content = BuildLayout();
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
                    new TextBlock { Text = "Videos are copied into Content Creation\\Media. The original file is left untouched." },
                    importButton,
                    _statusText,
                    new Separator(),
                    _mediaList
                }
            }
        };
        Grid.SetRow(media, 1);
        root.Children.Add(media);

        var preview = new Border
        {
            Margin = new Thickness(12, 0),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            Child = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "Preview",
                        FontSize = 22,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
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
                    new TextBlock { Text = "Select a clip to edit its properties." }
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
                    new TextBlock { Text = "Timeline", FontSize = 18, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = "Track 1   ─────────────────────────────────────────────", FontFamily = new FontFamily("Consolas") },
                    new TextBlock { Text = "Track 2   ─────────────────────────────────────────────", FontFamily = new FontFamily("Consolas") }
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
            var importer = new MediaImportService(_workspace);

            foreach (IStorageFile file in files)
            {
                string? localPath = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(localPath))
                {
                    continue;
                }

                MediaAsset asset = importer.Import(localPath);
                AddMediaItem(asset);
                imported++;
            }

            _statusText.Text = imported == 0
                ? "No local video files were imported."
                : $"Imported {imported} video{(imported == 1 ? string.Empty : "s")}.";
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            _statusText.Text = exception.Message;
        }
    }

    private void AddMediaItem(MediaAsset asset)
    {
        _mediaList.Children.Add(new TextBlock
        {
            Text = asset.FileName,
            TextWrapping = TextWrapping.Wrap,
            ToolTip = asset.LibraryPath
        });
    }
}