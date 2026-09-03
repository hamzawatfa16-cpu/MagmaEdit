using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MagmaEdit.PluginHost;

namespace MagmaEdit.App;

/// <summary>Provides explicit user approval and lifecycle controls for discovered plugins.</summary>
internal sealed class PluginManagerDialog : Window
{
    private readonly PluginRuntime _runtime;
    private readonly Action<string> _report;
    private readonly StackPanel _pluginList;

    public PluginManagerDialog(
        PluginRuntime runtime,
        Action<string> report)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(report);

        _runtime = runtime;
        _report = report;
        Title = "MagmaEdit Plugins";
        Width = 760;
        Height = 520;
        MinWidth = 620;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _pluginList = new StackPanel { Spacing = 10 };

        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        closeButton.Click += (_, _) => Close();

        Content = new Border
        {
            Padding = new Thickness(16),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                RowSpacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Plugins",
                                FontSize = 22,
                                FontWeight = Avalonia.Media.FontWeight.SemiBold
                            },
                            new TextBlock
                            {
                                Text = "Discovered plugins are never loaded automatically. Review the publisher and requested capabilities, then load only the plugins you approve.",
                                TextWrapping = TextWrapping.Wrap,
                                Opacity = 0.75
                            }
                        }
                    },
                    new ScrollViewer
                    {
                        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                        Content = _pluginList
                    },
                    closeButton
                }
            }
        };

        Grid.SetRow(((Grid)((Border)Content).Child!).Children[1], 1);
        Grid.SetRow(closeButton, 2);
        Refresh();
    }

    private void Refresh()
    {
        _pluginList.Children.Clear();

        if (_runtime.Discovery.Plugins.Count == 0 && _runtime.Discovery.Issues.Count == 0)
        {
            _pluginList.Children.Add(new TextBlock
            {
                Text = "No plugins were discovered in Content Creation\\Plugins.",
                Opacity = 0.75
            });
            return;
        }

        foreach (PluginDescriptor descriptor in _runtime.Discovery.Plugins)
        {
            bool loaded = _runtime.IsLoaded(descriptor.Manifest.Id);
            var loadButton = new Button
            {
                Content = loaded ? "Unload" : "Approve & Load",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            loadButton.Click += async (_, _) => await TogglePluginAsync(descriptor, loadButton);

            var details = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = descriptor.Manifest.Name,
                        FontSize = 16,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    new TextBlock { Text = $"Publisher: {descriptor.Manifest.Publisher}" },
                    new TextBlock { Text = $"Version: {descriptor.Manifest.Version}" },
                    new TextBlock { Text = $"ID: {descriptor.Manifest.Id}", Opacity = 0.65, TextWrapping = TextWrapping.Wrap },
                    new TextBlock
                    {
                        Text = $"Capabilities: {(descriptor.Manifest.Capabilities.Count == 0 ? "None" : string.Join(", ", descriptor.Manifest.Capabilities))}",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.75
                    }
                }
            };

            var card = new Border
            {
                Padding = new Thickness(12),
                BorderThickness = new Thickness(1),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        details,
                        loadButton
                    }
                }
            };

            Grid.SetColumn(loadButton, 1);
            _pluginList.Children.Add(card);
        }

        foreach (PluginDiscoveryIssue issue in _runtime.Discovery.Issues)
        {
            _pluginList.Children.Add(new Border
            {
                Padding = new Thickness(12),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = $"Discovery issue: {issue.AssemblyPath}{Environment.NewLine}{issue.Message}",
                    TextWrapping = TextWrapping.Wrap
                }
            });
        }
    }

    private async Task TogglePluginAsync(PluginDescriptor descriptor, Button button)
    {
        button.IsEnabled = false;
        try
        {
            if (_runtime.IsLoaded(descriptor.Manifest.Id))
            {
                await _runtime.UnloadPluginAsync(
                    descriptor.Manifest.Id,
                    _report).ConfigureAwait(true);
            }
            else
            {
                await _runtime.LoadPluginAsync(
                    descriptor,
                    _report).ConfigureAwait(true);
            }
        }
        finally
        {
            Refresh();
        }
    }
}
