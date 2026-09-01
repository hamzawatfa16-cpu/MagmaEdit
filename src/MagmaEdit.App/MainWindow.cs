using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.App;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "MagmaEdit";
        Width = 1280;
        Height = 800;
        MinWidth = 960;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        WorkspaceLayout layout = WorkspaceLayout.ForCurrentUser();
        new WorkspaceManager(layout).EnsureCreated();

        Content = BuildLayout(layout);
    }

    private static Control BuildLayout(WorkspaceLayout layout)
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,180"),
            ColumnDefinitions = new ColumnDefinitions("220,*,280"),
            Margin = new Thickness(12)
        };

        var header = new Border
        {
            Grid.ColumnSpan = 3,
            Padding = new Thickness(16, 12),
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = "MagmaEdit",
                FontSize = 24,
                FontWeight = FontWeight.SemiBold
            }
        };
        root.Children.Add(header);

        var media = new Border
        {
            Grid.Row = 1,
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Media", FontSize = 18, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = "Import videos into Content Creation\\Media." },
                    new Button { Content = "Import Video", HorizontalAlignment = HorizontalAlignment.Left },
                    new TextBlock { Text = layout.Media, TextWrapping = TextWrapping.Wrap, Opacity = 0.65 }
                }
            }
        };
        root.Children.Add(media);

        var preview = new Border
        {
            Grid.Row = 1,
            Grid.Column = 1,
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
        root.Children.Add(preview);

        var inspector = new Border
        {
            Grid.Row = 1,
            Grid.Column = 2,
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
        root.Children.Add(inspector);

        var timeline = new Border
        {
            Grid.Row = 2,
            Grid.ColumnSpan = 3,
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
        root.Children.Add(timeline);

        return root;
    }
}
