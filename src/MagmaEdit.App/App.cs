using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Themes.Fluent;

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
            var window = new MainWindow();
            _ = PreviewPlaybackController.Attach(window);
            MediaGalleryController gallery = MediaGalleryController.Attach(window);
            window.SetMediaGalleryController(gallery);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
