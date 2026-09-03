using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Themes.Fluent;
using MagmaEdit.Core.Media;
using MagmaEdit.Media.Sprocket;
using MagmaEdit.PluginHost;

namespace MagmaEdit.App;

public sealed class App : Application
{
    private PluginRuntime? _pluginRuntime;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            IMediaProbeService mediaProbeService = new SprocketMediaProbeService();
            var window = new MainWindow(mediaProbeService);
            desktop.MainWindow = window;
            desktop.Exit += async (_, _) => await DisposePluginRuntimeAsync();

            TryAttach("preview playback", () => _ = PreviewPlaybackController.Attach(window));
            TryAttach("media gallery", () =>
            {
                MediaGalleryController gallery = MediaGalleryController.Attach(window);
                window.SetMediaGalleryController(gallery);
            });
            TryAttach("export controller", () => _ = ExportController.Attach(window));
            TryAttach("update controller", () => _ = UpdateController.Attach(window));
            TryAttach("plugin runtime", () => StartPluginRuntime(window));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartPluginRuntime(MainWindow window)
    {
        _pluginRuntime = PluginRuntime.Create(window);
        foreach (PluginDiscoveryIssue issue in _pluginRuntime.Discovery.Issues)
        {
            StartupDiagnostics.WriteComponentFailure(
                "plugin discovery",
                new InvalidDataException($"{issue.AssemblyPath}: {issue.Message}"));
        }

        if (_pluginRuntime.Discovery.Plugins.Count > 0 || _pluginRuntime.Discovery.Issues.Count > 0)
        {
            window.Opened += async (_, _) => await ShowPluginManagerAsync(window);
        }
    }

    private async Task ShowPluginManagerAsync(MainWindow window)
    {
        if (_pluginRuntime is null)
        {
            return;
        }

        try
        {
            var dialog = new PluginManagerDialog(
                _pluginRuntime,
                window.SetStatusForGallery);
            await dialog.ShowDialog<object?>(window);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.WriteComponentFailure("plugin manager", exception);
        }
    }

    private async Task DisposePluginRuntimeAsync()
    {
        if (_pluginRuntime is null)
        {
            return;
        }

        try
        {
            await _pluginRuntime.DisposeAsync();
        }
        catch (Exception exception)
        {
            StartupDiagnostics.WriteComponentFailure("plugin shutdown", exception);
        }
        finally
        {
            _pluginRuntime = null;
        }
    }

    private static void TryAttach(string componentName, Action attach)
    {
        try
        {
            attach();
        }
        catch (Exception exception)
        {
            StartupDiagnostics.WriteComponentFailure(componentName, exception);
        }
    }
}
