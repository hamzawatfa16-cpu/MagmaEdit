using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Themes.Fluent;
using MagmaEdit.Core.Media;
using MagmaEdit.Media.Sprocket;

namespace MagmaEdit.App;

public sealed class App : Application
{
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

            TryAttach("preview playback", () => _ = PreviewPlaybackController.Attach(window));
            TryAttach("media gallery", () =>
            {
                MediaGalleryController gallery = MediaGalleryController.Attach(window);
                window.SetMediaGalleryController(gallery);
            });
            TryAttach("export controller", () => _ = ExportController.Attach(window));
            TryAttach("update controller", () => _ = UpdateController.Attach(window));
        }

        base.OnFrameworkInitializationCompleted();
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
